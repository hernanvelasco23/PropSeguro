using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;
using Tesseract;

namespace PropSeguro.Infrastructure.Documents;

public sealed class PdfOcrTextExtractor(IOptions<PdfOcrOptions> options) : IPdfOcrTextExtractor
{
    public async Task<PdfOcrResult> ExtractTextAsync(
        string filePath,
        DocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        var pdfToPpmPath = ResolvePdfToPpmPath(options.Value.PdfToPpmPath);

        if (pdfToPpmPath is null)
        {
            return new PdfOcrResult(
                string.Empty,
                TextExtractionQuality.Empty,
                OcrAttempted: false,
                "No se pudo ejecutar OCR porque no se encontró pdftoppm para renderizar el PDF.");
        }

        var tessDataPath = ResolveTessDataPath(options.Value.TessDataPath, options.Value.Language);

        if (tessDataPath is null)
        {
            return new PdfOcrResult(
                string.Empty,
                TextExtractionQuality.Empty,
                OcrAttempted: false,
                "No se pudo ejecutar OCR porque no se encontró la carpeta tessdata con idiomas de Tesseract. Configurá PdfOcr:TessDataPath o agregá eng.traineddata/spa.traineddata.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "propsegura-ocr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var imagePrefix = Path.Combine(tempDirectory, "page");
            var renderExitCode = await RunProcessAsync(
                pdfToPpmPath,
                $"-png -r {Math.Clamp(options.Value.Dpi, 120, 300)} -f 1 -l {Math.Clamp(options.Value.MaxPages, 1, 20)} \"{filePath}\" \"{imagePrefix}\"",
                cancellationToken);

            if (renderExitCode != 0)
            {
                return new PdfOcrResult(
                    string.Empty,
                    TextExtractionQuality.Empty,
                    OcrAttempted: true,
                    "No se pudo renderizar el PDF para OCR.");
            }

            var imageFiles = Directory.GetFiles(tempDirectory, "page-*.png")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (imageFiles.Length == 0)
            {
                return new PdfOcrResult(
                    string.Empty,
                    TextExtractionQuality.Empty,
                    OcrAttempted: true,
                    "No se generaron imágenes para OCR.");
            }

            var pageTexts = new List<string>();
            using var engine = CreateEngine(tessDataPath, options.Value.Language);

            foreach (var imageFile in imageFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var image = Pix.LoadFromFile(imageFile);
                using var page = engine.Process(image);
                var pageText = page.GetText()?.Trim();

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    pageTexts.Add(pageText);
                }
            }

            var usefulPages = pageTexts
                .Where(text => !DocumentTextQualityEvaluator.IsLikelyAuditPage(documentType, text))
                .ToArray();
            var selectedPages = usefulPages.Length > 0 ? usefulPages : pageTexts.ToArray();
            var combinedText = JoinPages(selectedPages);
            var quality = DocumentTextQualityEvaluator.Evaluate(documentType, combinedText);

            return new PdfOcrResult(
                combinedText,
                quality,
                OcrAttempted: true,
                string.IsNullOrWhiteSpace(combinedText)
                    ? "El PDF parece escaneado, pero OCR no pudo obtener texto útil. Revisá y pegá el texto manualmente antes de analizar."
                    : "El PDF parece escaneado. PropSegura intentó leerlo con OCR. Revisá el texto antes de analizar.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new PdfOcrResult(
                string.Empty,
                TextExtractionQuality.Empty,
                OcrAttempted: true,
                "No se pudo ejecutar OCR sobre el PDF. Revisá y pegá el texto manualmente antes de analizar.");
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // Temporary OCR files are best-effort cleanup.
            }
        }
    }

    private static TesseractEngine CreateEngine(string tessDataPath, string configuredLanguage)
    {
        var languages = string.IsNullOrWhiteSpace(configuredLanguage)
            ? ["spa+eng", "spa", "eng"]
            : new[] { configuredLanguage, "spa+eng", "spa", "eng" }.Distinct().ToArray();

        Exception? lastException = null;

        foreach (var language in languages)
        {
            try
            {
                return new TesseractEngine(tessDataPath, language, EngineMode.Default);
            }
            catch (Exception exception)
            {
                lastException = exception;
            }
        }

        throw new InvalidOperationException("No se pudo inicializar Tesseract con los idiomas configurados.", lastException);
    }

    private static async Task<int> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode;
    }

    private static string JoinPages(IEnumerable<string> pages)
    {
        var builder = new StringBuilder();
        var pageNumber = 1;

        foreach (var page in pages)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.AppendLine($"--- Página {pageNumber} ---");
            builder.AppendLine(page);
            pageNumber++;
        }

        return builder.ToString().Trim();
    }

    private static string? ResolvePdfToPpmPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var pathMatch = FindExecutableOnPath("pdftoppm.exe") ?? FindExecutableOnPath("pdftoppm");

        if (pathMatch is not null)
        {
            return pathMatch;
        }

        var knownWindowsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "MiKTeX",
            "miktex",
            "bin",
            "x64",
            "pdftoppm.exe");

        return File.Exists(knownWindowsPath) ? knownWindowsPath : null;
    }

    private static string? ResolveTessDataPath(string? configuredPath, string configuredLanguage)
    {
        foreach (var candidatePath in GetTessDataPathCandidates(configuredPath))
        {
            if (HasAnyRequestedLanguageData(candidatePath, configuredLanguage))
            {
                return candidatePath;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetTessDataPathCandidates(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            foreach (var path in ExpandTessDataCandidate(configuredPath))
            {
                yield return path;
            }

            if (!Path.IsPathRooted(configuredPath))
            {
                foreach (var path in ExpandTessDataCandidate(Path.Combine(AppContext.BaseDirectory, configuredPath)))
                {
                    yield return path;
                }

                foreach (var path in ExpandTessDataCandidate(Path.GetFullPath(configuredPath)))
                {
                    yield return path;
                }
            }
        }

        foreach (var path in ExpandTessDataCandidate(Path.Combine(AppContext.BaseDirectory, "tessdata")))
        {
            yield return path;
        }

        foreach (var path in FindTessDataDirectoriesUpwards(Directory.GetCurrentDirectory()))
        {
            yield return path;
        }

        var environmentPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");

        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            foreach (var path in ExpandTessDataCandidate(environmentPath))
            {
                yield return path;
            }
        }

        foreach (var path in ExpandTessDataCandidate(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Tesseract-OCR",
            "tessdata")))
        {
            yield return path;
        }

        foreach (var path in ExpandTessDataCandidate(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Tesseract-OCR",
            "tessdata")))
        {
            yield return path;
        }
    }

    private static IEnumerable<string> ExpandTessDataCandidate(string path)
    {
        if (Directory.Exists(path))
        {
            yield return path;
        }

        var childTessDataPath = Path.Combine(path, "tessdata");

        if (Directory.Exists(childTessDataPath))
        {
            yield return childTessDataPath;
        }
    }

    private static IEnumerable<string> FindTessDataDirectoriesUpwards(string startPath)
    {
        var directory = new DirectoryInfo(startPath);

        while (directory is not null)
        {
            var tessDataPath = Path.Combine(directory.FullName, "tessdata");

            if (Directory.Exists(tessDataPath))
            {
                yield return tessDataPath;
            }

            directory = directory.Parent;
        }
    }

    private static bool HasAnyRequestedLanguageData(string path, string configuredLanguage)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        var languages = string.IsNullOrWhiteSpace(configuredLanguage)
            ? ["spa", "eng"]
            : configuredLanguage
                .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(["spa", "eng"])
                .Distinct(StringComparer.OrdinalIgnoreCase);

        return languages.Any(language => File.Exists(Path.Combine(path, $"{language}.traineddata")));
    }

    private static string? FindExecutableOnPath(string executableName)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return paths
            .Select(path => Path.Combine(path, executableName))
            .FirstOrDefault(File.Exists);
    }
}

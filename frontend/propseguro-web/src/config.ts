const LOCAL_API_BASE_URL = 'http://localhost:5288';

function trimTrailingSlash(value: string) {
  return value.replace(/\/$/, '');
}

function isLocalhost() {
  const hostname = window.location.hostname;

  return hostname === 'localhost' ||
    hostname === '127.0.0.1' ||
    hostname === '[::1]';
}

function resolveApiBaseUrl() {
  const configuredApiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim();

  if (configuredApiBaseUrl) {
    return trimTrailingSlash(configuredApiBaseUrl);
  }

  if (isLocalhost()) {
    return LOCAL_API_BASE_URL;
  }

  return '';
}

export const API_BASE_URL = resolveApiBaseUrl();

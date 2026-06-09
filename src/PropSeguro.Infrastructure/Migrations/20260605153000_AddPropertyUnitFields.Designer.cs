using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PropSeguro.Infrastructure.Persistence;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260605153000_AddPropertyUnitFields")]
    partial class AddPropertyUnitFields
    {
    }
}

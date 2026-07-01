using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// No-op migration. The <c>Clients</c>, <c>ClientCredentials</c>, <c>ClientCertificates</c>,
    /// and <c>ServiceAccounts</c> tables were already dropped by
    /// <see cref="DropLegacyApplicationTables"/>. The EF model just never stopped declaring the
    /// corresponding entities/DbSets until now, so the scaffolded diff wrongly re-proposed dropping
    /// (and, on Down, recreating) tables that no longer exist. This migration exists solely to bring
    /// the model snapshot back in sync with reality; it performs no schema changes.
    /// </summary>
    public partial class RemoveLegacyClientServiceAccountEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

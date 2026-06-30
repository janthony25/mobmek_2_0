using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceDueDateToDateOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // timestamptz -> date needs an explicit USING cast in PostgreSQL.
            migrationBuilder.Sql(
                @"ALTER TABLE ""Invoices"" ALTER COLUMN ""DueDate"" TYPE date USING ""DueDate""::date;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""Invoices"" ALTER COLUMN ""DueDate"" TYPE timestamp with time zone USING ""DueDate""::timestamp with time zone;");
        }
    }
}

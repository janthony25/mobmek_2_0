using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Odometer = table.Column<int>(type: "integer", nullable: false),
                    JobNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InvoiceNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TotalJobPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalJobProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jobs_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TradePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RetailPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MarkupSolution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Markup = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ItemQuantity = table.Column<int>(type: "integer", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ItemTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobItems_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobMechanics",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobMechanics", x => new { x.JobId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_JobMechanics_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobMechanics_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Labour",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RatePerHour = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labour", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Labour_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobServiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobServiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobServiceLines_JobServices_JobServiceId",
                        column: x => x.JobServiceId,
                        principalTable: "JobServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobServiceLines_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobItems_JobId",
                table: "JobItems",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobMechanics_EmployeeId",
                table: "JobMechanics",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CarId",
                table: "Jobs",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CustomerId",
                table: "Jobs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_JobServiceLines_JobId",
                table: "JobServiceLines",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobServiceLines_JobServiceId",
                table: "JobServiceLines",
                column: "JobServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Labour_JobId",
                table: "Labour",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobItems");

            migrationBuilder.DropTable(
                name: "JobMechanics");

            migrationBuilder.DropTable(
                name: "JobServiceLines");

            migrationBuilder.DropTable(
                name: "Labour");

            migrationBuilder.DropTable(
                name: "JobServices");

            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}

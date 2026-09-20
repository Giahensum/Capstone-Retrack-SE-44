using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_configs",
                columns: table => new
                {
                    config_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    config_value = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configs", x => x.config_key);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "depots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    rating = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_depots", x => x.id);
                    table.ForeignKey(
                        name: "FK_depots_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "factories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    rating = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factories", x => x.id);
                    table.ForeignKey(
                        name: "FK_factories_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "depot_staffs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_depot_staffs", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_staffs_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_depot_staffs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pickup_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_depot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted_collector_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    request_image_url = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    preferred_datetime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    checkin_image_url = table.Column<string>(type: "text", nullable: true),
                    gross_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    platform_fee_percentage = table.Column<decimal>(type: "numeric", nullable: false),
                    platform_fee_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    payment_proof_url = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pickup_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_pickup_requests_depots_target_depot_id",
                        column: x => x.target_depot_id,
                        principalTable: "depots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_pickup_requests_users_accepted_collector_id",
                        column: x => x.accepted_collector_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pickup_requests_users_seller_id",
                        column: x => x.seller_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "factory_demands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    factory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    required_weight_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    min_price_per_kg = table.Column<decimal>(type: "numeric", nullable: true),
                    max_price_per_kg = table.Column<decimal>(type: "numeric", nullable: true),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factory_demands", x => x.id);
                    table.ForeignKey(
                        name: "FK_factory_demands_factories_factory_id",
                        column: x => x.factory_id,
                        principalTable: "factories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "factory_depot_partnerships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factory_depot_partnerships", x => x.id);
                    table.ForeignKey(
                        name: "FK_factory_depot_partnerships_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_factory_depot_partnerships_factories_factory_id",
                        column: x => x.factory_id,
                        principalTable: "factories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_factory_id = table.Column<Guid>(type: "uuid", nullable: true),
                    material_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    declared_weight_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_batches_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_batches_factories_target_factory_id",
                        column: x => x.target_factory_id,
                        principalTable: "factories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "pickup_request_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pickup_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    price_per_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    sub_total = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pickup_request_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_pickup_request_items_pickup_requests_pickup_request_id",
                        column: x => x.pickup_request_id,
                        principalTable: "pickup_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seller_depot_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pickup_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_depot_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_seller_depot_reviews_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_seller_depot_reviews_pickup_requests_pickup_request_id",
                        column: x => x.pickup_request_id,
                        principalTable: "pickup_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "batch_quality_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actual_weight_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    grade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    agreed_price_per_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    platform_fee_percentage = table.Column<decimal>(type: "numeric", nullable: false),
                    platform_fee_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    payment_proof_url = table.Column<string>(type: "text", nullable: true),
                    is_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_quality_checks", x => x.id);
                    table.ForeignKey(
                        name: "FK_batch_quality_checks_factories_factory_id",
                        column: x => x.factory_id,
                        principalTable: "factories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_batch_quality_checks_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "factory_depot_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factory_depot_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_factory_depot_reviews_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_factory_depot_reviews_factories_factory_id",
                        column: x => x.factory_id,
                        principalTable: "factories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_factory_depot_reviews_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transport_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    checkin_depot_image_url = table.Column<string>(type: "text", nullable: true),
                    checkout_factory_image_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transport_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_transport_jobs_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transport_jobs_users_driver_id",
                        column: x => x.driver_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "system_configs",
                columns: new[] { "config_key", "config_value", "description", "updated_at" },
                values: new object[] { "PLATFORM_FEE_PERCENTAGE", "1.00", "Phí n?n t?ng 1%", new DateTime(2026, 9, 20, 7, 49, 15, 238, DateTimeKind.Utc).AddTicks(399) });

            migrationBuilder.CreateIndex(
                name: "IX_batch_quality_checks_batch_id",
                table: "batch_quality_checks",
                column: "batch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_batch_quality_checks_factory_id",
                table: "batch_quality_checks",
                column: "factory_id");

            migrationBuilder.CreateIndex(
                name: "IX_depot_staffs_depot_id",
                table: "depot_staffs",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_depot_staffs_user_id",
                table: "depot_staffs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_depots_owner_id",
                table: "depots",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_factories_owner_id",
                table: "factories",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_factory_demands_factory_id",
                table: "factory_demands",
                column: "factory_id");

            migrationBuilder.CreateIndex(
                name: "IX_factory_depot_partnerships_depot_id_factory_id",
                table: "factory_depot_partnerships",
                columns: new[] { "depot_id", "factory_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_factory_depot_partnerships_factory_id",
                table: "factory_depot_partnerships",
                column: "factory_id");

            migrationBuilder.CreateIndex(
                name: "IX_factory_depot_reviews_batch_id",
                table: "factory_depot_reviews",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_factory_depot_reviews_depot_id",
                table: "factory_depot_reviews",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_factory_depot_reviews_factory_id",
                table: "factory_depot_reviews",
                column: "factory_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_batches_depot_id",
                table: "inventory_batches",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_batches_target_factory_id",
                table: "inventory_batches",
                column: "target_factory_id");

            migrationBuilder.CreateIndex(
                name: "IX_pickup_request_items_pickup_request_id",
                table: "pickup_request_items",
                column: "pickup_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_pickup_requests_accepted_collector_id",
                table: "pickup_requests",
                column: "accepted_collector_id");

            migrationBuilder.CreateIndex(
                name: "IX_pickup_requests_seller_id",
                table: "pickup_requests",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "IX_pickup_requests_target_depot_id",
                table: "pickup_requests",
                column: "target_depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_seller_depot_reviews_depot_id",
                table: "seller_depot_reviews",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_seller_depot_reviews_pickup_request_id",
                table: "seller_depot_reviews",
                column: "pickup_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_transport_jobs_batch_id",
                table: "transport_jobs",
                column: "batch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transport_jobs_driver_id",
                table: "transport_jobs",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batch_quality_checks");

            migrationBuilder.DropTable(
                name: "depot_staffs");

            migrationBuilder.DropTable(
                name: "factory_demands");

            migrationBuilder.DropTable(
                name: "factory_depot_partnerships");

            migrationBuilder.DropTable(
                name: "factory_depot_reviews");

            migrationBuilder.DropTable(
                name: "pickup_request_items");

            migrationBuilder.DropTable(
                name: "platform_transactions");

            migrationBuilder.DropTable(
                name: "seller_depot_reviews");

            migrationBuilder.DropTable(
                name: "system_configs");

            migrationBuilder.DropTable(
                name: "transport_jobs");

            migrationBuilder.DropTable(
                name: "pickup_requests");

            migrationBuilder.DropTable(
                name: "inventory_batches");

            migrationBuilder.DropTable(
                name: "depots");

            migrationBuilder.DropTable(
                name: "factories");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}


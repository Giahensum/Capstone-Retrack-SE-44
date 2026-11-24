using System.Text.Json.Serialization;

namespace Retrack.API.DTOs
{
    public class AiPredictionItemDto
    {
        [JsonPropertyName("class_key")]
        public string ClassKey { get; set; } = string.Empty;

        [JsonPropertyName("name_vi")]
        public string NameVi { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("material_type")]
        public string MaterialType { get; set; } = string.Empty;

        [JsonPropertyName("is_recyclable")]
        public bool IsRecyclable { get; set; }
    }

    public class AiBoundingBoxDto
    {
        [JsonPropertyName("x1")]
        public int X1 { get; set; }

        [JsonPropertyName("y1")]
        public int Y1 { get; set; }

        [JsonPropertyName("x2")]
        public int X2 { get; set; }

        [JsonPropertyName("y2")]
        public int Y2 { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("image_width")]
        public int? ImageWidth { get; set; }

        [JsonPropertyName("image_height")]
        public int? ImageHeight { get; set; }
    }

    public class AiDetectedObjectDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("class_key")]
        public string ClassKey { get; set; } = string.Empty;

        [JsonPropertyName("name_vi")]
        public string NameVi { get; set; } = string.Empty;

        [JsonPropertyName("material_type")]
        public string MaterialType { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("visual_area_percent")]
        public double VisualAreaPercent { get; set; }

        [JsonPropertyName("physical_weight_percent")]
        public double PhysicalWeightPercent { get; set; }

        [JsonPropertyName("estimated_share_percent")]
        public double EstimatedSharePercent { get; set; }

        [JsonPropertyName("bulk_density")]
        public double BulkDensity { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#10b981";

        [JsonPropertyName("is_recyclable")]
        public bool IsRecyclable { get; set; } = true;

        [JsonPropertyName("price_range")]
        public string PriceRange { get; set; } = string.Empty;

        [JsonPropertyName("current_market_price")]
        public decimal? CurrentMarketPrice { get; set; }

        [JsonPropertyName("bounding_box")]
        public AiBoundingBoxDto? BoundingBox { get; set; }
    }

    public class SellerAiClassifyResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        [JsonPropertyName("scan_mode")]
        public string ScanMode { get; set; } = "single";

        [JsonPropertyName("primary_class")]
        public string PrimaryClass { get; set; } = string.Empty;

        [JsonPropertyName("name_vi")]
        public string NameVi { get; set; } = string.Empty;

        [JsonPropertyName("material_type")]
        public string MaterialType { get; set; } = string.Empty;

        [JsonPropertyName("is_recyclable")]
        public bool IsRecyclable { get; set; } = true;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("box_status")]
        public string BoxStatus { get; set; } = string.Empty;

        [JsonPropertyName("bounding_box")]
        public AiBoundingBoxDto? BoundingBox { get; set; }

        [JsonPropertyName("ocr_text")]
        public string OcrText { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;

        [JsonPropertyName("unit")]
        public string Unit { get; set; } = string.Empty;

        [JsonPropertyName("price_range")]
        public string PriceRange { get; set; } = string.Empty;

        [JsonPropertyName("current_market_price")]
        public decimal? CurrentMarketPrice { get; set; }

        [JsonPropertyName("recycling_tip")]
        public string RecyclingTip { get; set; } = string.Empty;

        [JsonPropertyName("suggested_description")]
        public string SuggestedDescription { get; set; } = string.Empty;

        [JsonPropertyName("top_predictions")]
        public List<AiPredictionItemDto> TopPredictions { get; set; } = new();

        [JsonPropertyName("detected_objects")]
        public List<AiDetectedObjectDto> DetectedObjects { get; set; } = new();

        [JsonPropertyName("mixed_summary")]
        public string MixedSummary { get; set; } = string.Empty;

        [JsonPropertyName("annotated_image")]
        public string AnnotatedImage { get; set; } = string.Empty;

        [JsonPropertyName("uploaded_url")]
        public string? UploadedUrl { get; set; }
    }

    public class AiClassifyBase64RequestDto
    {
        [JsonPropertyName("image_base64")]
        public string ImageBase64 { get; set; } = string.Empty;

        [JsonPropertyName("upload_to_cloud")]
        public bool UploadToCloud { get; set; } = false;

        [JsonPropertyName("mode")]
        public string? Mode { get; set; } = "single";
    }

    public class PriceForecastPointDto
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("day_label")]
        public string DayLabel { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("min_price")]
        public decimal MinPrice { get; set; }

        [JsonPropertyName("max_price")]
        public decimal MaxPrice { get; set; }

        [JsonPropertyName("is_forecast")]
        public bool IsForecast { get; set; }
    }

    public class MaterialPriceForecastDto
    {
        [JsonPropertyName("material_type")]
        public string MaterialType { get; set; } = string.Empty;

        [JsonPropertyName("material_name_vi")]
        public string MaterialNameVi { get; set; } = string.Empty;

        [JsonPropertyName("unit")]
        public string Unit { get; set; } = "kg";

        [JsonPropertyName("current_price")]
        public decimal CurrentPrice { get; set; }

        [JsonPropertyName("projected_price_7d")]
        public decimal ProjectedPrice7d { get; set; }

        [JsonPropertyName("projected_change_pct")]
        public decimal ProjectedChangePct { get; set; }

        [JsonPropertyName("trend")]
        public string Trend { get; set; } = "STABLE"; // UPWARD | STABLE | DOWNWARD

        [JsonPropertyName("recommendation_code")]
        public string RecommendationCode { get; set; } = "NEUTRAL"; // SELL_NOW | HOLD_ACCUMULATE | NEUTRAL

        [JsonPropertyName("recommendation_title")]
        public string RecommendationTitle { get; set; } = string.Empty;

        [JsonPropertyName("recommendation_detail")]
        public string RecommendationDetail { get; set; } = string.Empty;

        [JsonPropertyName("optimal_time_window")]
        public string OptimalTimeWindow { get; set; } = string.Empty;

        [JsonPropertyName("estimated_profit_increase_pct")]
        public decimal EstimatedProfitIncreasePct { get; set; }

        [JsonPropertyName("factory_demand_weight_kg")]
        public decimal FactoryDemandWeightKg { get; set; }

        [JsonPropertyName("active_depots_buying")]
        public int ActiveDepotsBuying { get; set; }

        [JsonPropertyName("timeline")]
        public List<PriceForecastPointDto> Timeline { get; set; } = new();
    }
}

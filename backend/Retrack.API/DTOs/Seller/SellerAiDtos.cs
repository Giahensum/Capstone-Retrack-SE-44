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
    }

    public class SellerAiClassifyResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

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
    }
}

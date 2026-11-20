namespace Retrack.API.Services.Money;

/// <summary>Định dạng số tiền VND trong thông báo tiếng Việt: dấu chấm phân cách hàng nghìn (không phụ thuộc dữ liệu văn hóa của máy chủ).</summary>
public static class MoneyFormat
{
    public static string Vnd(decimal value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
}

using System.Text;

namespace Retrack.API.Services.ESign;

/// <summary>
/// Cấu hình DocuSign (môi trường sandbox cho demo đồ án). Nạp từ biến môi trường <c>DocuSign__*</c>; để trống thì tính năng ký điện tử tắt,
/// phần còn lại của hệ thống vẫn chạy bình thường. Không đưa khóa vào mã nguồn hay Git.
/// </summary>
public sealed class DocuSignOptions
{
    /// <summary>Integration Key của ứng dụng (Admin &gt; Apps and Keys).</summary>
    public string IntegrationKey { get; set; } = "";
    /// <summary>User ID (API Username, dạng GUID) của tài khoản được ký thay.</summary>
    public string UserId { get; set; } = "";
    /// <summary>API Account ID (GUID).</summary>
    public string AccountId { get; set; } = "";
    /// <summary>Địa chỉ gốc của API: sandbox là https://demo.docusign.net, production là địa chỉ base URI của tài khoản.</summary>
    public string BaseUri { get; set; } = "https://demo.docusign.net";
    /// <summary>Máy chủ OAuth: sandbox là account-d.docusign.com, production là account.docusign.com.</summary>
    public string OAuthHost { get; set; } = "account-d.docusign.com";
    /// <summary>Khóa riêng RSA (PEM) mã hóa base64 để nằm gọn trong một dòng biến môi trường.</summary>
    public string PrivateKeyBase64 { get; set; } = "";
    /// <summary>Địa chỉ https công khai DocuSign gọi khi hợp đồng có thay đổi (không bắt buộc: không có thì dựa vào nút "Cập nhật trạng thái").</summary>
    public string WebhookUrl { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(IntegrationKey) && !string.IsNullOrWhiteSpace(UserId)
        && !string.IsNullOrWhiteSpace(AccountId) && !string.IsNullOrWhiteSpace(PrivateKeyBase64);

    public string ReadPrivateKeyPem()
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(PrivateKeyBase64.Trim())); }
        catch (FormatException) { throw new DocuSignException("DocuSign__PrivateKeyBase64 không phải chuỗi base64 hợp lệ."); }
    }
}

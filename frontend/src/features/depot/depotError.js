export function depotError(error) {
  const status = error?.response?.status;
  const body = error?.response?.data;
  const fields = Object.values(body?.errors ?? {}).flat().filter(value => typeof value === 'string');
  let message;
  if (error?.code === 'ECONNABORTED' || error?.code === 'ETIMEDOUT') {
    message = 'Hết thời gian chờ. Hãy kiểm tra kết quả trước khi gửi lại thao tác.';
  } else if (error?.code === 'ERR_CANCELED') {
    message = 'Thao tác đã được hủy.';
  } else if (!error?.response) {
    message = 'Không kết nối được máy chủ. Hãy kiểm tra mạng và thử lại.';
  } else if (status === 503 && body?.code === 'VERIFICATION_SCANNER_UNAVAILABLE') {
    message = 'Dịch vụ kiểm tra an toàn tệp chưa sẵn sàng. Hồ sơ chưa được nộp; vui lòng thử lại sau.';
  } else if (status >= 500) {
    message = status === 502 || status === 503 ? 'Dịch vụ kết nối đang gián đoạn. Vui lòng thử lại sau.'
      : status === 504 ? 'Dịch vụ chưa phản hồi kịp. Hãy kiểm tra kết quả trước khi gửi lại thao tác.'
        : 'Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.';
  } else if (status === 401) {
    message = 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  } else if (status === 429) {
    message = 'Bạn thao tác quá nhanh. Hãy chờ một lát rồi thử lại.';
  } else {
    message = fields.join(' ') || (typeof body?.message === 'string' && body.message)
      || (typeof body?.detail === 'string' && body.detail)
      || (status === 403 ? 'Bạn không có quyền thực hiện thao tác này.'
        : status === 404 ? 'Không tìm thấy dữ liệu. Hãy tải lại danh sách.'
          : 'Không thể thực hiện thao tác. Vui lòng kiểm tra dữ liệu và thử lại.');
  }
  const traceId = body?.traceId;
  return typeof traceId === 'string' && /^[a-zA-Z0-9:._-]{1,128}$/.test(traceId)
    ? `${message} Mã tra cứu ${traceId}.` : message;
}

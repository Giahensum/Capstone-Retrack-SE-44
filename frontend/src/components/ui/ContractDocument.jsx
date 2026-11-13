import './contract-document.css';

const statusLabels = {
  PENDING_DEPOT: 'Chờ chủ vựa ký',
  ACTIVE: 'Đang hiệu lực',
  REJECTED: 'Đã từ chối',
  TERMINATED: 'Đã chấm dứt',
};

const formatDate = (value, includeTime = true) => value
  ? new Intl.DateTimeFormat('vi-VN', includeTime
    ? { dateStyle: 'medium', timeStyle: 'short' }
    : { dateStyle: 'medium' }).format(new Date(value))
  : 'Chưa ghi nhận';

function Signature({ title, name, signedAt }) {
  return <div className={`contract-signature ${signedAt ? 'is-signed' : ''}`}>
    <span>{title}</span>
    <strong>{signedAt ? 'ĐÃ KÝ ĐIỆN TỬ' : 'CHƯA KÝ'}</strong>
    <p>{name}</p>
    <small>{formatDate(signedAt)}</small>
  </div>;
}

export default function ContractDocument({ contract, factoryName = 'Nhà máy trên RETRACK', depotName = 'Chủ vựa trên RETRACK' }) {
  const paragraphs = String(contract.contractTerms || '')
    .split(/\n\s*\n/)
    .map((part) => part.trim())
    .filter(Boolean);

  return <article className="contract-print-sheet" aria-label="Nội dung hợp đồng điện tử">
    <header className="contract-letterhead">
      <div><strong>RETRACK</strong><span>Nền tảng truy xuất và thu mua tái chế</span></div>
      <div className={`contract-status status-${String(contract.contractStatus || '').toLowerCase()}`}>
        {statusLabels[contract.contractStatus] || contract.contractStatus || 'Chưa xác định'}
      </div>
    </header>

    <div className="contract-national-heading">
      <strong>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</strong>
      <span>Độc lập – Tự do – Hạnh phúc</span>
    </div>

    <h2>HỢP ĐỒNG HỢP TÁC THU MUA<br />NGUYÊN LIỆU TÁI CHẾ</h2>
    <p className="contract-code">Số: {contract.contractCode || 'Chưa cấp'} · Phiên bản {contract.contractVersion || 1}</p>

    <section className="contract-parties">
      <div><span>BÊN MUA (NHÀ MÁY)</span><strong>{factoryName}</strong><small>Xác thực qua tài khoản RETRACK</small></div>
      <div><span>BÊN CUNG CẤP (CHỦ VỰA)</span><strong>{depotName}</strong><small>Xác thực qua tài khoản RETRACK</small></div>
    </section>

    <section className="contract-terms">
      {paragraphs.map((paragraph, index) => <div key={`${index}-${paragraph.slice(0, 20)}`}>
        <h3>Điều {index + 1}</h3>
        <p>{paragraph}</p>
      </div>)}
    </section>

    <section className="contract-timeline">
      <h3>Hiệu lực và lịch sử xác nhận</h3>
      <dl>
        <div><dt>Nhà máy ký</dt><dd>{formatDate(contract.factorySignedAt)}</dd></div>
        <div><dt>Chủ vựa ký</dt><dd>{formatDate(contract.depotSignedAt)}</dd></div>
        <div><dt>Ngày hiệu lực</dt><dd>{formatDate(contract.contractEffectiveAt)}</dd></div>
        <div><dt>Ngày hết hạn</dt><dd>{formatDate(contract.contractExpiresAt, false)}</dd></div>
        {contract.terminatedAt && <div><dt>Ngày chấm dứt</dt><dd>{formatDate(contract.terminatedAt)}</dd></div>}
      </dl>
      {contract.terminationReason && <p className="contract-termination"><strong>Lý do chấm dứt:</strong> {contract.terminationReason}</p>}
    </section>

    <section className="contract-signatures">
      <Signature title="ĐẠI DIỆN BÊN MUA" name={factoryName} signedAt={contract.factorySignedAt} />
      <Signature title="ĐẠI DIỆN BÊN CUNG CẤP" name={depotName} signedAt={contract.depotSignedAt} />
    </section>

    <footer>
      <span>Tài liệu điện tử được ghi nhận trên RETRACK</span>
      <span>Mã kiểm chứng SHA-256</span>
      <code>{contract.contractTermsHash || 'Chưa tạo mã kiểm chứng'}</code>
    </footer>
  </article>;
}

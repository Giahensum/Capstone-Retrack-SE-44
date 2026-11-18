import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, Empty, Field, Modal, PageHead, Pagination, readAttachment, useFactory } from "../components/FactoryUI";
import { money } from "../data/factoryState";
import { downloadFactoryAttachment, request, uploadFactoryFeeProof } from "../data/factoryApi";

const statuses = { UNPAID: "Chưa thanh toán", SUBMITTED: "Chờ Admin đối soát", PAID: "Đã thanh toán" };
const date = (value) => value ? new Date(value).toLocaleDateString("vi-VN") : "—";
const orderCodeOf = (invoice) => (invoice?.payOsOrderCode == null ? null : String(invoice.payOsOrderCode));
const transferReference = (invoice) => `RETRACK ${invoice.periodStart?.slice(0, 7)?.replace("-", "")} ${invoice.id.slice(0, 8).toUpperCase()}`;

export default function FactoryPlatformFees() {
  const [section, setSection] = useState("invoices");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState(null);
  const [proof, setProof] = useState("");
  const [uploading, setUploading] = useState(false);
  const { notify } = useFactory();
  const client = useQueryClient();
  const account = useQuery({ queryKey: ["factory", "platform-fees", "payment-account"], queryFn: () => request("/api/factory/platform-fees/payment-account") });
  const invoices = useQuery({ queryKey: ["factory", "platform-fees", "invoices", page], queryFn: () => request(`/api/factory/platform-fees/invoices?page=${page}&pageSize=20`) });
  const fees = useQuery({ queryKey: ["factory", "platform-fees", "transactions", page], queryFn: () => request(`/api/factory/platform-fees/transactions?page=${page}&pageSize=20`), enabled: section === "transactions" });
  const mutation = useMutation({
    mutationFn: ({ id, path, method = "POST", body }) => request(`/api/factory/platform-fees/invoices/${id}/${path}`, { method, body }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["factory", "platform-fees"] }),
  });

  const payosReturn = new URLSearchParams(window.location.search);
  const payosStatus = payosReturn.get("payos");
  const returnedOrder = payosStatus === "return" ? payosReturn.get("orderCode") : null;
  const [syncMessage, setSyncMessage] = useState("");
  const payos = useMutation({
    mutationFn: ({ id, path }) => request(`/api/factory/platform-fees/invoices/${id}/${path}`, { method: "POST" }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["factory", "platform-fees"] }),
  });
  async function payWithPayos(invoice) {
    try {
      const link = await payos.mutateAsync({ id: invoice.id, path: "payos-link" });
      window.location.assign(link.checkoutUrl);
    } catch (error) { notify(error.message, true); }
  }
  // Sau khi PayOS chuyển hướng về: hỏi lại PayOS (không tin tham số trên URL) rồi hiển thị kết quả.
  const returnedInvoice = returnedOrder ? invoices.data?.items?.find((i) => orderCodeOf(i) === returnedOrder) : null;
  const returnedId = returnedInvoice?.id;
  const returnedStatus = returnedInvoice?.status;
  useEffect(() => {
    if (!returnedId || returnedStatus === "PAID") return;
    payos.mutateAsync({ id: returnedId, path: "payos-sync" }).then((status) => setSyncMessage(status === "PAID" ? "paid" : "pending")).catch((error) => notify(error.message, true));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [returnedId, returnedStatus]);
  // Có hóa đơn đã tạo link PayOS mà chưa thanh toán: backend tự đối soát nên tải lại mỗi 10 giây.
  const waitingPayos = section === "invoices" && invoices.data?.items?.some((i) => i.status === "UNPAID" && orderCodeOf(i));
  useEffect(() => {
    if (!waitingPayos) return undefined;
    const timer = setInterval(() => client.invalidateQueries({ queryKey: ["factory", "platform-fees"] }), 10000);
    return () => clearInterval(timer);
  }, [waitingPayos, client]);

  const data = section === "invoices" ? invoices.data : fees.data;
  const query = section === "invoices" ? invoices : fees;
  const busy = mutation.isPending || uploading || payos.isPending;
  async function upload(file) {
    if (!file) return;
    setUploading(true);
    try {
      await readAttachment(file);
      setProof(await uploadFactoryFeeProof(file));
      notify("Đã tải chứng từ lên. Hãy kiểm tra rồi gửi xác nhận.");
    } catch (error) { notify(error.message, true); }
    finally { setUploading(false); }
  }
  async function submit(event) {
    event.preventDefault();
    try {
      if (!proof) throw new Error("Hãy tải chứng từ chuyển khoản trước khi gửi.");
      await mutation.mutateAsync({ id: selected.id, path: "confirmation", method: "PATCH", body: { paymentProofUrl: proof } });
      setSelected(null); setProof(""); notify("Đã gửi chứng từ; hóa đơn đang chờ Admin đối soát.");
    } catch (error) { notify(error.message, true); }
  }
  return <>
    <PageHead title="Phí nền tảng" text="Theo dõi phí thu mua và thanh toán hóa đơn do hệ thống phát hành." />
    {returnedOrder && <p role="status" className="factory-notice">{payos.isPending ? "Đang xác nhận thanh toán với PayOS…" : syncMessage === "paid" || returnedStatus === "PAID" ? "Thanh toán PayOS thành công, hóa đơn đã được ghi nhận." : "PayOS chưa ghi nhận thanh toán. Nếu bạn đã chuyển tiền, hệ thống sẽ tự cập nhật khi nhận thông báo từ PayOS."}</p>}
    {payosStatus === "cancel" && <p role="status" className="factory-notice">Bạn đã hủy thanh toán PayOS. Hóa đơn vẫn chờ thanh toán.</p>}
    <Card>
      <p>Với lô mới, Factory trả phí nền tảng riêng. Tiền hàng và VAT theo hóa đơn được chuyển cho vựa. Hóa đơn phí chỉ phát hành sau khi tháng kết thúc theo giờ Việt Nam; phí tháng hiện tại là tạm tính.</p>
      <p>Admin phát hành hóa đơn theo tháng. Chuyển khoản đúng số tiền và nội dung ghi trên hóa đơn, sau đó tải chứng từ để Admin đối soát. Gửi chứng từ chưa đồng nghĩa đã thanh toán.</p>
    </Card>
    <Card title="Tài khoản nhận phí của hệ thống">
      {account.isPending ? <p role="status">Đang tải thông tin chuyển khoản…</p> : account.error ? <div role="alert">{account.error.message} <Button secondary onClick={() => account.refetch()}>Thử lại</Button></div> : account.data?.isConfigured ? <dl className="financial"><div><dt>Ngân hàng</dt><dd>{account.data.bankName}</dd></div><div><dt>Số tài khoản</dt><dd>{account.data.accountNumber}</dd></div><div><dt>Chủ tài khoản</dt><dd>{account.data.accountHolder}</dd></div></dl> : <p role="status">Admin chưa cấu hình tài khoản nhận phí. Chưa thực hiện chuyển khoản cho đến khi thông tin được công bố tại đây.</p>}
    </Card>
    <nav className="factory-fee-tabs" aria-label="Nội dung phí nền tảng">
      <Button secondary={section !== "invoices"} aria-current={section === "invoices" ? "page" : undefined} onClick={() => { setSection("invoices"); setPage(1); }}>Hóa đơn tháng</Button>
      <Button secondary={section !== "transactions"} aria-current={section === "transactions" ? "page" : undefined} onClick={() => { setSection("transactions"); setPage(1); }}>Phí từng lô</Button>
    </nav>
    <Card title={section === "invoices" ? "Hóa đơn phí nền tảng" : "Phí Factory trả riêng theo lô"}>
      {query.isPending ? <p role="status">Đang tải dữ liệu…</p> : query.error ? <div role="alert">{query.error.message} <Button secondary onClick={() => query.refetch()}>Thử lại</Button></div> : !data?.items?.length ? <Empty text={section === "invoices" ? "Admin chưa phát hành hóa đơn phí." : "Chưa có phí Factory trả riêng từ lô mới."} /> : <div className="table-wrap"><table className="factory-fee-table"><thead><tr>{(section === "invoices" ? ["Kỳ", "Số tiền", "Trạng thái", "Thao tác"] : ["Lô", "Ngày ghi nhận", "Phí nền tảng"]).map((heading) => <th key={heading}>{heading}</th>)}</tr></thead><tbody>
        {section === "invoices" ? data.items.map((invoice) => <tr key={invoice.id}><td>{date(invoice.periodStart)}<small>{invoice.revision > 0 ? ` · Bổ sung lần ${invoice.revision}` : ' · Hóa đơn gốc'}</small></td><td>{money(invoice.amount)}</td><td>{statuses[invoice.status] || invoice.status}</td><td>{invoice.status === "UNPAID" ? <div className="actions"><Button disabled={busy} onClick={() => payWithPayos(invoice)}>{orderCodeOf(invoice) ? "Tiếp tục thanh toán PayOS" : "Thanh toán PayOS"}</Button>{orderCodeOf(invoice) ? <small>Đang chờ PayOS xác nhận; không gửi chứng từ thủ công cho hóa đơn này.</small> : account.data?.isConfigured ? <Button secondary disabled={busy} onClick={() => { setSelected(invoice); setProof(""); }}>Gửi chứng từ chuyển khoản</Button> : <small>Chờ Admin cấu hình tài khoản</small>}</div> : invoice.status === "SUBMITTED" ? "Đang đối soát" : "Đã xác nhận"}</td></tr>) : data.items.map((fee) => <tr key={fee.id}><td>{fee.batchCode || fee.id.slice(0, 8)}</td><td>{date(fee.date)}</td><td>{money(fee.feeAmount)}</td></tr>)}
      </tbody></table></div>}
      <Pagination page={page} totalCount={data?.totalCount || 0} totalPages={Math.ceil((data?.totalCount || 0) / 20)} onChange={setPage} />
    </Card>
    {selected && <Modal title="Gửi chứng từ chuyển khoản" onClose={() => setSelected(null)} busy={busy}><form onSubmit={submit}><p>{selected.revision > 0 ? `Hóa đơn bổ sung lần ${selected.revision}` : 'Hóa đơn gốc'} kỳ {date(selected.periodStart)}: <strong>{money(selected.amount)}</strong></p><p>Chuyển vào <strong>{account.data?.accountNumber} · {account.data?.bankName}</strong>, tên <strong>{account.data?.accountHolder}</strong>.</p><p>Nội dung chuyển khoản: <strong>{transferReference(selected)}</strong>. Sau khi chuyển khoản, tải chứng từ và gửi để Admin đối soát.</p><Field label="Ảnh hoặc PDF chứng từ"><input type="file" accept=".pdf,.jpg,.jpeg,.png" disabled={busy} onChange={(event) => { upload(event.target.files?.[0]); event.target.value = ""; }} /></Field><p>PDF, JPG hoặc PNG, tối đa 700 KB. {proof ? "Đã tải tệp lên." : "Chưa có chứng từ."}</p>{proof && <Button secondary onClick={() => downloadFactoryAttachment(proof, "chung-tu-chuyen-khoan")}>Xem chứng từ</Button>}<div className="actions"><Button secondary disabled={busy} onClick={() => setSelected(null)}>Hủy</Button><button className="btn" type="submit" disabled={busy || !proof}>{busy ? "Đang gửi…" : "Gửi xác nhận"}</button></div></form></Modal>}
  </>;
}

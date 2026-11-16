import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, Empty, Field, Modal, PageHead, Pagination, useFactory } from "../components/FactoryUI";
import { money } from "../data/factoryState";
import { request } from "../data/factoryApi";

const statuses = { UNPAID: "Chưa thanh toán", SUBMITTED: "Chờ Admin đối soát", PAID: "Đã thanh toán" };
const orderCodeOf = (url) => { try { return new URL(url).searchParams.get("orderCode"); } catch { return null; } };
const date = (value) => value ? new Date(value).toLocaleDateString("vi-VN") : "—";

export default function FactoryPlatformFees() {
  const [section, setSection] = useState("invoices");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState(null);
  const [proof, setProof] = useState("");
  const [message, setMessage] = useState("");
  const { notify } = useFactory();
  const client = useQueryClient();
  const returned = new URLSearchParams(window.location.search).get("payos") === "return";
  const returnCode = new URLSearchParams(window.location.search).get("orderCode");
  const synced = useRef(false);
  const invoices = useQuery({ queryKey: ["factory", "platform-fees", "invoices", page], queryFn: () => request(`/api/factory/platform-fees/invoices?page=${page}&pageSize=20`) });
  const fees = useQuery({ queryKey: ["factory", "platform-fees", "transactions", page], queryFn: () => request(`/api/factory/platform-fees/transactions?page=${page}&pageSize=20`), enabled: section === "transactions" });
  const mutation = useMutation({
    mutationFn: ({ id, path, method = "POST", body }) => request(`/api/factory/platform-fees/invoices/${id}/${path}`, { method, body }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["factory", "platform-fees"] }),
  });

  useEffect(() => {
    if (!returned || !returnCode || synced.current || !invoices.data) return;
    const invoice = invoices.data.items.find((item) => orderCodeOf(item.paymentProofUrl) === returnCode);
    if (!invoice) { setMessage("Chưa tìm thấy hóa đơn PayOS trong trang này. Hãy mở hóa đơn tương ứng để kiểm tra lại."); return; }
    synced.current = true;
    request(`/api/factory/platform-fees/invoices/${invoice.id}/payos-sync`, { method: "POST" })
      .then((result) => setMessage(result === "PAID" ? "PayOS đã xác nhận thanh toán hóa đơn." : "PayOS chưa xác nhận thanh toán. Hệ thống sẽ tiếp tục đối soát."))
      .catch((error) => setMessage(error.message))
      .finally(() => client.invalidateQueries({ queryKey: ["factory", "platform-fees"] }));
  }, [returned, returnCode, invoices.data, client]);

  const data = section === "invoices" ? invoices.data : fees.data;
  const query = section === "invoices" ? invoices : fees;
  const busy = mutation.isPending;
  async function pay(invoice) {
    try {
      const link = await mutation.mutateAsync({ id: invoice.id, path: "payos-link" });
      window.location.assign(link.checkoutUrl);
    } catch (error) { notify(error.message, true); client.invalidateQueries({ queryKey: ["factory", "platform-fees"] }); }
  }
  async function submit(event) {
    event.preventDefault();
    try {
      await mutation.mutateAsync({ id: selected.id, path: "confirmation", method: "PATCH", body: { paymentProofUrl: proof } });
      setSelected(null); setProof(""); notify("Đã gửi chứng từ; hóa đơn đang chờ Admin đối soát.");
    } catch (error) { notify(error.message, true); }
  }
  return <>
    <PageHead title="Phí nền tảng" text="Theo dõi phí thu mua và thanh toán hóa đơn do hệ thống phát hành." />
    <Card>
      <p>Với lô mới, Factory trả phí nền tảng riêng. Tiền hàng và VAT theo hóa đơn được chuyển cho vựa. Hóa đơn phí do Admin phát hành theo tháng.</p>
      <p>Chuyển khoản theo thông tin do Admin cung cấp rồi gửi chứng từ, hoặc dùng PayOS khi đã được cấu hình. Gửi chứng từ chưa đồng nghĩa đã thanh toán.</p>
    </Card>
    <nav className="factory-fee-tabs" aria-label="Nội dung phí nền tảng">
      <Button secondary={section !== "invoices"} aria-current={section === "invoices" ? "page" : undefined} onClick={() => { setSection("invoices"); setPage(1); }}>Hóa đơn tháng</Button>
      <Button secondary={section !== "transactions"} aria-current={section === "transactions" ? "page" : undefined} onClick={() => { setSection("transactions"); setPage(1); }}>Phí từng lô</Button>
    </nav>
    {message && <p role="status" className="info-box">{message}</p>}
    {new URLSearchParams(window.location.search).get("payos") === "cancel" && <p role="status" className="info-box">Bạn đã hủy thanh toán PayOS. Hóa đơn vẫn chờ thanh toán.</p>}
    <Card title={section === "invoices" ? "Hóa đơn phí nền tảng" : "Phí Factory trả riêng theo lô"}>
      {query.isPending ? <p role="status">Đang tải dữ liệu…</p> : query.error ? <div role="alert">{query.error.message} <Button secondary onClick={() => query.refetch()}>Thử lại</Button></div> : !data?.items?.length ? <Empty text={section === "invoices" ? "Admin chưa phát hành hóa đơn phí." : "Chưa có phí Factory trả riêng từ lô mới."} /> : <div className="table-wrap"><table className="factory-fee-table"><thead><tr>{(section === "invoices" ? ["Kỳ", "Số tiền", "Trạng thái", "Thao tác"] : ["Lô", "Ngày ghi nhận", "Phí nền tảng"]).map((heading) => <th key={heading}>{heading}</th>)}</tr></thead><tbody>
        {section === "invoices" ? data.items.map((invoice) => <tr key={invoice.id}><td>{date(invoice.periodStart)}</td><td>{money(invoice.amount)}</td><td>{statuses[invoice.status] || invoice.status}</td><td>{invoice.status === "UNPAID" ? <div className="factory-fee-actions"><Button disabled={busy} onClick={() => pay(invoice)}>{orderCodeOf(invoice.paymentProofUrl) ? "Tiếp tục PayOS" : "Thanh toán PayOS"}</Button>{!orderCodeOf(invoice.paymentProofUrl) && <Button secondary disabled={busy} onClick={() => { setSelected(invoice); setProof(""); }}>Gửi chứng từ chuyển khoản</Button>}</div> : invoice.status === "SUBMITTED" ? "Đang đối soát" : "Đã xác nhận"}</td></tr>) : data.items.map((fee) => <tr key={fee.id}><td>{fee.batchCode || fee.id.slice(0, 8)}</td><td>{date(fee.date)}</td><td>{money(fee.feeAmount)}</td></tr>)}
      </tbody></table></div>}
      <Pagination page={page} totalCount={data?.totalCount || 0} totalPages={Math.ceil((data?.totalCount || 0) / 20)} onChange={setPage} />
    </Card>
    {selected && <Modal title="Gửi chứng từ chuyển khoản" onClose={() => setSelected(null)} busy={busy}><form onSubmit={submit}><p>Hóa đơn {date(selected.periodStart)}: <strong>{money(selected.amount)}</strong></p><p>Chỉ gửi sau khi bạn đã chuyển khoản theo hướng dẫn của Admin. Hóa đơn sẽ chờ Admin xác nhận.</p><Field label="Đường dẫn chứng từ"><input type="url" required pattern="https?://.+" value={proof} onChange={(event) => setProof(event.target.value)} placeholder="https://..." /></Field><div className="actions"><Button secondary disabled={busy} onClick={() => setSelected(null)}>Hủy</Button><button className="btn" type="submit" disabled={busy}>{busy ? "Đang gửi…" : "Gửi xác nhận"}</button></div></form></Modal>}
  </>;
}

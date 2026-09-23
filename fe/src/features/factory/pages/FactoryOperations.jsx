import { useState } from "react";
import {
  Attachment,
  Button,
  Card,
  Empty,
  Field,
  Modal,
  PageHead,
  Status,
  useFactory,
} from "../components/FactoryUI";
import {
  labels,
  materials,
  money,
  number,
  settlement,
  weigh,
} from "../data/factoryState";

const modes = {
  orders: {
    title: "Đơn hàng & Vận chuyển",
    text: "Theo dõi từng lô từ lúc nhận đơn đến khi tài xế giao tại nhà máy.",
    states: Object.keys(labels).filter(
      (s) => !["APPROVED", "PENDING", "BLOCKED"].includes(s),
    ),
  },
  qc: {
    title: "Trạm cân & Kiểm soát chất lượng",
    text: "Xác nhận nhận hàng → Cân Gross/Tare → Kiểm tra chất lượng → Nghiệm thu.",
    states: ["DELIVERED", "RECEIVED", "WEIGHED", "VERIFIED", "REJECTED"],
  },
  settlements: {
    title: "Quyết toán thu mua",
    text: "Chốt đơn giá sau KCS, khấu trừ phí nền tảng và ghi nhận thanh toán cho vựa.",
    states: ["VERIFIED", "PAID"],
  },
};
export function Operations({ mode, focusId, navigate }) {
  const { state, act } = useFactory();
  const config = modes[mode];
  const [selectedId, setSelectedId] = useState(focusId || null);
  const [filter, setFilter] = useState("");
  const [search, setSearch] = useState("");
  const [action, setAction] = useState(null);
  const orders = state.orders.filter(
    (o) =>
      config.states.includes(o.status) &&
      (!filter || o.status === filter) &&
      `${o.id} ${o.batchId} ${state.depots.find((d) => d.id === o.depotId)?.name}`
        .toLowerCase()
        .includes(search.toLowerCase()),
  );
  const order = state.orders.find((o) => o.id === selectedId);
  const depot = order && state.depots.find((d) => d.id === order.depotId);
  return (
    <>
      <PageHead title={config.title} text={config.text} />
      {mode === "settlements" && (
        <div className="info-box">
          <strong>
            Phí nền tảng tích lũy:{" "}
            {money(state.orders.reduce((n, o) => n + (o.payment?.fee || 0), 0))}
          </strong>
          <span>
            Phí được giữ lại từ tiền hàng và chờ đối soát công nợ. Bản demo chưa
            có hóa đơn thu phí từ Admin.
          </span>
        </div>
      )}
      <div className="filter-bar">
        <Field
          label="Tìm đơn / vựa"
          value={search}
          placeholder="Nhập mã đơn, mã lô hoặc tên vựa"
          onChange={(e) => setSearch(e.target.value)}
        />
        <Field label="Trạng thái">
          <select value={filter} onChange={(e) => setFilter(e.target.value)}>
            <option value="">Tất cả trạng thái</option>
            {config.states.map((s) => (
              <option key={s} value={s}>
                {labels[s]}
              </option>
            ))}
          </select>
        </Field>
      </div>
      <Card title={`${orders.length} đơn hàng`}>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Đơn / Lô hàng</th>
                <th>Vựa / Nguyên liệu</th>
                <th>Khai báo / Thực cân</th>
                <th>Trạng thái</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {orders.map((o) => (
                <tr
                  key={o.id}
                  className={selectedId === o.id ? "selected-row" : ""}
                >
                  <td>
                    <strong>{o.id}</strong>
                    <small>{o.batchId}</small>
                  </td>
                  <td>
                    {state.depots.find((d) => d.id === o.depotId)?.name}
                    <small>{materials[o.material]}</small>
                  </td>
                  <td>
                    {number(o.kg)} kg
                    <small>
                      {o.weight
                        ? `${number(o.weight.net)} kg thực cân`
                        : "Chưa cân tại nhà máy"}
                    </small>
                  </td>
                  <td>
                    <Status value={o.status} />
                  </td>
                  <td>
                    <Button secondary onClick={() => setSelectedId(o.id)}>
                      Chi tiết →
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!orders.length && <Empty text="Chưa có đơn phù hợp" />}
        </div>
      </Card>
      {order && (
        <section className="order-detail">
          <div className="section-toolbar">
            <div>
              <span className="eyebrow">HỒ SƠ LÔ HÀNG</span>
              <h2>
                {order.id} <span className="muted">/ {order.batchId}</span>
              </h2>
            </div>
            <Button secondary onClick={() => setSelectedId(null)}>
              Đóng chi tiết
            </Button>
          </div>
          <div className="two-columns detail-columns">
            <Card
              title={materials[order.material]}
              action={<Status value={order.status} />}
            >
              <dl className="detail-list">
                <div>
                  <dt>Vựa cung cấp</dt>
                  <dd>{depot.name}</dd>
                </div>
                <div>
                  <dt>Liên hệ</dt>
                  <dd>{depot.phone}</dd>
                </div>
                <div>
                  <dt>Địa điểm giao</dt>
                  <dd>{state.profile.address}</dd>
                </div>
                <div>
                  <dt>Tài xế</dt>
                  <dd>
                    {order.status === "ACCEPTED"
                      ? "Chưa nhận chuyến"
                      : "Tài xế demo • 51C-123.45"}
                  </dd>
                </div>
                <div>
                  <dt>Khối lượng vựa</dt>
                  <dd>{number(order.kg)} kg</dd>
                </div>
                {order.weight && (
                  <>
                    <div>
                      <dt>Gross / Tare</dt>
                      <dd>
                        {number(order.weight.gross)} /{" "}
                        {number(order.weight.tare)} kg
                      </dd>
                    </div>
                    <div>
                      <dt>Khối lượng thực</dt>
                      <dd>{number(order.weight.net)} kg</dd>
                    </div>
                    <div>
                      <dt>Chênh lệch</dt>
                      <dd className={order.weight.flagged ? "text-danger" : ""}>
                        {number(order.weight.difference)}%{" "}
                        {order.weight.flagged && "• Vượt 5%"}
                      </dd>
                    </div>
                  </>
                )}
              </dl>
              {order.weight?.note && (
                <p className="info-box">Biên bản cân: {order.weight.note}</p>
              )}
              {order.weight?.attachment && (
                <a
                  href={order.weight.attachment.data}
                  download={order.weight.attachment.name}
                >
                  ↓ Phiếu cân: {order.weight.attachment.name}
                </a>
              )}
              {order.qc && (
                <div className="quality-summary">
                  <strong>KCS • Grade {order.qc.grade}</strong>
                  <p>
                    Tinh khiết {order.qc.purity}% · Độ ẩm {order.qc.moisture}% ·
                    Tạp chất {order.qc.contamination}%
                  </p>
                  {order.qc.note && <p>{order.qc.note}</p>}
                  {order.status === "REJECTED" && (
                    <p className="text-danger">
                      Hướng xử lý:{" "}
                      {order.qc.resolution === "RETURN"
                        ? "Trả hàng về vựa"
                        : "Liên hệ vựa để thỏa thuận lại"}
                      . Đơn không đủ điều kiện quyết toán.
                    </p>
                  )}
                </div>
              )}
              <div className="actions">
                {order.status === "DELIVERED" && (
                  <Button
                    onClick={() =>
                      act(
                        "RECEIVE",
                        { id: order.id },
                        "Đã xác nhận xe giao hàng. Có thể lập phiếu cân.",
                      )
                    }
                  >
                    Xác nhận nhận hàng
                  </Button>
                )}
                {["RECEIVED", "WEIGHED"].includes(order.status) && (
                  <Button onClick={() => setAction("weigh")}>
                    {order.weight ? "Sửa phiếu cân" : "Lập phiếu cân"}
                  </Button>
                )}
                {order.status === "WEIGHED" && (
                  <Button onClick={() => setAction("qc")}>
                    Kiểm tra & Chốt KCS
                  </Button>
                )}
                {order.status === "VERIFIED" && (
                  <Button onClick={() => setAction("settle")}>
                    Thỏa thuận giá & Quyết toán
                  </Button>
                )}
                {order.status === "PAID" && !order.rating && (
                  <Button onClick={() => setAction("rate")}>
                    Đánh giá lô & Hợp tác
                  </Button>
                )}
              </div>
              {["ACCEPTED", "IN_TRANSIT"].includes(order.status) && (
                <div className="simulation-box">
                  <strong>Mô phỏng cập nhật từ tài xế</strong>
                  <p>
                    Chỉ dùng thử luồng trong bản demo; không phải thao tác vận
                    chuyển của nhà máy.
                  </p>
                  <Button
                    secondary
                    onClick={() =>
                      act(
                        "SIMULATE_TRANSPORT",
                        { id: order.id },
                        "Đã cập nhật trạng thái vận chuyển mô phỏng.",
                      )
                    }
                  >
                    {order.status === "ACCEPTED"
                      ? "Demo: Tài xế nhận & lấy hàng"
                      : "Demo: Tài xế đã giao hàng"}
                  </Button>
                </div>
              )}
            </Card>
            <Card title="Lịch sử xử lý">
              <ol className="timeline">
                {order.history.map((h, i) => (
                  <li key={i}>
                    <span className="timeline-dot" />
                    <div>
                      <strong>{labels[h.status]}</strong>
                      <small>{new Date(h.at).toLocaleString("vi-VN")}</small>
                    </div>
                  </li>
                ))}
              </ol>
              <p className="muted">
                Theo dõi các mốc xử lý; không theo dõi GPS liên tục.
              </p>
            </Card>
          </div>
          {order.payment && (
            <Card title="Bảng kê đã quyết toán">
              <Financial values={order.payment} net={order.weight.net} />
              <p className="muted">
                Mã tham chiếu: {order.payment.reference} ·{" "}
                {new Date(order.payment.at).toLocaleString("vi-VN")}
              </p>
            </Card>
          )}
          {["VERIFIED", "PAID"].includes(order.status) && (
            <Card title="Hóa đơn đính kèm">
              <Attachment
                key={order.id}
                label="Đính kèm hóa đơn"
                value={order.invoice}
                onChange={(file) =>
                  act(
                    "INVOICE",
                    { id: order.id, file },
                    "Đã lưu tệp hóa đơn trong bản demo.",
                  )
                }
              />
            </Card>
          )}
          {order.rating && (
            <Card title="Đánh giá sau quyết toán">
              <p>
                <strong>★ {order.rating.stars}/5</strong> ·{" "}
                {order.rating.comment || "Không có nhận xét."}
              </p>
              <Button secondary onClick={() => navigate("partners")}>
                Xem quan hệ đối tác →
              </Button>
            </Card>
          )}
        </section>
      )}
      {order && action && (
        <Modal
          title={
            {
              weigh: `Phiếu cân • ${order.id}`,
              qc: `Kiểm tra chất lượng • ${order.id}`,
              settle: `Quyết toán • ${order.id}`,
              rate: `Đánh giá lô • ${order.id}`,
            }[action]
          }
          onClose={() => setAction(null)}
        >
          {action === "weigh" && (
            <WeighForm
              key={order.id}
              order={order}
              close={() => setAction(null)}
            />
          )}
          {action === "qc" && (
            <QualityForm
              key={order.id}
              order={order}
              close={() => setAction(null)}
            />
          )}
          {action === "settle" && (
            <SettlementForm
              key={order.id}
              order={order}
              close={() => setAction(null)}
            />
          )}
          {action === "rate" && (
            <RatingForm
              key={order.id}
              order={order}
              close={() => setAction(null)}
            />
          )}
        </Modal>
      )}
    </>
  );
}
function Financial({ values, net }) {
  return (
    <dl className="financial">
      <div>
        <dt>Khối lượng nghiệm thu</dt>
        <dd>{number(net)} kg</dd>
      </div>
      <div>
        <dt>Đơn giá thỏa thuận</dt>
        <dd>{money(values.price)}/kg</dd>
      </div>
      <div>
        <dt>Tổng tiền hàng</dt>
        <dd>{money(values.total)}</dd>
      </div>
      <div>
        <dt>Phí nền tảng giữ lại (5%)</dt>
        <dd>− {money(values.fee)}</dd>
      </div>
      <div className="financial-total">
        <dt>Thực trả cho vựa</dt>
        <dd>{money(values.payable)}</dd>
      </div>
    </dl>
  );
}
function WeighForm({ order, close }) {
  const { act } = useFactory();
  const [form, setForm] = useState({
    gross: order.weight?.gross ?? "",
    tare: order.weight?.tare ?? "",
    note: order.weight?.note || "",
    attachment: order.weight?.attachment,
  });
  const set = (key, value) => setForm((f) => ({ ...f, [key]: value }));
  let result;
  try {
    result = weigh(form.gross, form.tare, order.kg);
  } catch {
    /* Show preview only for valid readings. */
  }
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (
          act(
            "WEIGH",
            { id: order.id, ...form },
            "Đã lưu phiếu cân. Lô hàng sẵn sàng kiểm tra KCS.",
          )
        )
          close();
      }}
    >
      <div className="info-box">
        Vựa khai báo <strong>{number(order.kg)} kg</strong>. Cảnh báo khi chênh
        lệch tuyệt đối lớn hơn 5%.
      </div>
      <div className="form-grid">
        <Field
          label="Gross • Xe có hàng (kg)"
          required
          type="number"
          min="0.01"
          step="any"
          value={form.gross}
          onChange={(e) => set("gross", e.target.value)}
        />
        <Field
          label="Tare • Xe rỗng (kg)"
          required
          type="number"
          min="0"
          step="any"
          value={form.tare}
          onChange={(e) => set("tare", e.target.value)}
        />
      </div>
      {result && (
        <div className={`weight-preview ${result.flagged ? "warning" : ""}`}>
          <div>
            <small>KHỐI LƯỢNG TỊNH</small>
            <strong>{number(result.net)} kg</strong>
          </div>
          <div>
            <small>CHÊNH LỆCH</small>
            <strong>{number(result.difference)}%</strong>
          </div>
        </div>
      )}
      <Field
        label={`Biên bản / Ghi chú${result?.flagged ? " (bắt buộc khi lệch trên 5%)" : ""}`}
      >
        <textarea
          required={result?.flagged}
          value={form.note}
          onChange={(e) => set("note", e.target.value)}
        />
      </Field>
      <Attachment
        label="Ảnh hoặc PDF phiếu cân"
        value={form.attachment}
        onChange={(file) => set("attachment", file)}
      />
      <div className="form-actions">
        <Button secondary onClick={close}>
          Hủy
        </Button>
        <Button type="submit">Lưu phiếu cân</Button>
      </div>
    </form>
  );
}
function QualityForm({ order, close }) {
  const { act, state } = useFactory();
  const [form, setForm] = useState({
    purity: "",
    moisture: "",
    contamination: "",
    grade: "",
    decision: "accept",
    note: "",
    resolution: "RETURN",
  });
  const set = (key, value) => setForm((f) => ({ ...f, [key]: value }));
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (
          act(
            "QC",
            { id: order.id, ...form },
            form.decision === "accept"
              ? "KCS đạt. Đã chuyển lô sang chờ quyết toán."
              : "Đã từ chối lô và ghi nhận hướng xử lý.",
          )
        )
          close();
      }}
    >
      <div className="info-box">
        Khối lượng thực cân: <strong>{number(order.weight.net)} kg</strong>. Sau
        khi chốt, phiếu cân và KCS được khóa.
      </div>
      <div className="form-grid">
        {[
          ["purity", "Độ tinh khiết (%)"],
          ["moisture", "Độ ẩm (%)"],
          ["contamination", "Tạp chất (%)"],
        ].map(([key, label]) => (
          <Field
            key={key}
            label={label}
            required
            type="number"
            min="0"
            max="100"
            step=".01"
            value={form[key]}
            onChange={(e) => set(key, e.target.value)}
          />
        ))}
        <Field label="Grade">
          <select
            required
            value={form.grade}
            onChange={(e) => set("grade", e.target.value)}
          >
            <option value="">Chọn phẩm cấp</option>
            {["A", "B", "C"].map((g) => (
              <option key={g}>{g}</option>
            ))}
          </select>
        </Field>
      </div>
      {form.purity !== "" &&
        Number(form.purity) < Number(state.profile.purity) && (
          <div className="info-box warning">
            Độ tinh khiết thấp hơn yêu cầu hồ sơ ({state.profile.purity}%). Cân
            nhắc từ chối hoặc ghi rõ căn cứ nghiệm thu.
          </div>
        )}
      <Field label="Quyết định">
        <select
          value={form.decision}
          onChange={(e) => set("decision", e.target.value)}
        >
          <option value="accept">Chấp nhận • Chuyển quyết toán</option>
          <option value="reject">Từ chối lô</option>
        </select>
      </Field>
      {form.decision === "reject" && (
        <Field label="Hướng xử lý">
          <select
            value={form.resolution}
            onChange={(e) => set("resolution", e.target.value)}
          >
            <option value="RETURN">Trả hàng về vựa</option>
            <option value="RENEGOTIATE">Liên hệ vựa để thỏa thuận lại</option>
          </select>
        </Field>
      )}
      <Field
        label={
          form.decision === "reject"
            ? "Lý do từ chối (bắt buộc)"
            : "Ghi chú nghiệm thu"
        }
      >
        <textarea
          required={form.decision === "reject"}
          value={form.note}
          onChange={(e) => set("note", e.target.value)}
        />
      </Field>
      <div className="form-actions">
        <Button secondary onClick={close}>
          Hủy
        </Button>
        <Button type="submit" danger={form.decision === "reject"}>
          Chốt kết quả KCS
        </Button>
      </div>
    </form>
  );
}
function SettlementForm({ order, close }) {
  const { act } = useFactory();
  const [price, setPrice] = useState("");
  const [reference, setReference] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  let preview;
  try {
    preview = settlement(order.weight.net, Number(price));
  } catch {
    /* Price has not been entered. */
  }
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (
          confirmed &&
          act(
            "SETTLE",
            { id: order.id, price, reference },
            "Đã ghi nhận quyết toán demo. Có thể đánh giá lô hàng.",
          )
        )
          close();
      }}
    >
      <div className="info-box">
        KCS đạt · Grade {order.qc.grade}. Đây là ghi nhận xác nhận chuyển khoản,
        không thực hiện giao dịch ngân hàng.
      </div>
      <Field
        label="Đơn giá đã thỏa thuận sau KCS (đ/kg)"
        required
        type="number"
        min="1"
        step="1"
        value={price}
        onChange={(e) => setPrice(e.target.value)}
      />
      {preview && <Financial values={preview} net={order.weight.net} />}
      <Field
        label="Mã tham chiếu chuyển khoản"
        required
        maxLength={200}
        value={reference}
        onChange={(e) => setReference(e.target.value)}
      />
      <label className="checkbox-line">
        <input
          type="checkbox"
          required
          checked={confirmed}
          onChange={(e) => setConfirmed(e.target.checked)}
        />
        Tôi xác nhận giá đã thống nhất và đã chuyển số tiền thực trả cho vựa (mô
        phỏng).
      </label>
      <div className="form-actions">
        <Button secondary onClick={close}>
          Hủy
        </Button>
        <Button type="submit" disabled={!preview || !confirmed}>
          Ghi nhận quyết toán
        </Button>
      </div>
    </form>
  );
}
function RatingForm({ order, close }) {
  const { act } = useFactory();
  const [form, setForm] = useState({
    stars: "5",
    comment: "",
    partnership: "APPROVED",
  });
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (
          act(
            "RATE",
            { id: order.id, ...form },
            "Đã lưu đánh giá lô và quyết định hợp tác.",
          )
        )
          close();
      }}
    >
      <p>
        Đánh giá gắn với đơn <strong>{order.id}</strong> đã quyết toán.
      </p>
      <Field label="Chất lượng lô hàng">
        <select
          value={form.stars}
          onChange={(e) => setForm({ ...form, stars: e.target.value })}
        >
          {[5, 4, 3, 2, 1].map((n) => (
            <option value={n} key={n}>
              {n} sao
            </option>
          ))}
        </select>
      </Field>
      <Field label="Nhận xét">
        <textarea
          value={form.comment}
          onChange={(e) => setForm({ ...form, comment: e.target.value })}
        />
      </Field>
      <Field label="Hợp tác những lần tiếp theo">
        <select
          value={form.partnership}
          onChange={(e) => setForm({ ...form, partnership: e.target.value })}
        >
          <option value="APPROVED">
            Tiếp tục hợp tác • Cho phép chỉ định trực tiếp
          </option>
          <option value="BLOCKED">Ngừng hợp tác • Chặn lô mới từ vựa</option>
        </select>
      </Field>
      <div className="form-actions">
        <Button secondary onClick={close}>
          Hủy
        </Button>
        <Button type="submit">Lưu đánh giá</Button>
      </div>
    </form>
  );
}

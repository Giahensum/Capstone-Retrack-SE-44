import { useState } from "react";
import {
  Attachment,
  Button,
  Card,
  Confirm,
  Empty,
  Field,
  Modal,
  PageHead,
  Status,
  useFactory,
} from "../components/FactoryUI";
import { materials, money, number, today } from "../data/factoryState";

const MaterialSelect = ({ value, onChange, all = false }) => (
  <select value={value} onChange={onChange}>
    {all && <option value="">Tất cả vật liệu</option>}
    {Object.entries(materials).map(([key, name]) => (
      <option key={key} value={key}>
        {name}
      </option>
    ))}
  </select>
);
export function Dashboard({ navigate }) {
  const { state } = useFactory();
  const [period, setPeriod] = useState("month");
  const start = new Date();
  start.setHours(0, 0, 0, 0);
  if (period === "month") start.setDate(1);
  else if (period === "year") {
    start.setMonth(0, 1);
  }
  const paid = state.orders.filter(
    (o) => o.status === "PAID" && new Date(o.payment.at) >= start,
  );
  const weight = paid.reduce((n, o) => n + o.weight.net, 0);
  const spend = paid.reduce((n, o) => n + o.payment.payable, 0);
  const fees = paid.reduce((n, o) => n + o.payment.fee, 0);
  const pending = state.orders.filter((o) =>
    ["DELIVERED", "RECEIVED", "WEIGHED"].includes(o.status),
  );
  const ready = state.orders.filter((o) => o.status === "VERIFIED");
  const volume = Object.keys(materials)
    .map((material) => ({
      material,
      kg: paid
        .filter((o) => o.material === material)
        .reduce((n, o) => n + o.weight.net, 0),
    }))
    .filter((x) => x.kg);
  const max = Math.max(1, ...volume.map((x) => x.kg));
  const trend = Array.from({ length: 6 }, (_, i) => {
    const date = new Date();
    date.setDate(1);
    date.setMonth(date.getMonth() - 5 + i);
    const items = state.orders.filter(
      (o) =>
        o.status === "PAID" &&
        new Date(o.payment.at).getMonth() === date.getMonth() &&
        new Date(o.payment.at).getFullYear() === date.getFullYear(),
    );
    return {
      label: `T${date.getMonth() + 1}`,
      value: items.reduce((n, o) => n + o.payment.payable, 0),
    };
  });
  return (
    <>
      <PageHead
        eyebrow="TỔNG QUAN VẬN HÀNH"
        title="Chào ngày mới, GreenCycle."
        text="Theo dõi nguồn hàng, chất lượng và chi phí thu mua tại một nơi."
      >
        <Button onClick={() => navigate("marketplace")}>
          + Tìm nguồn nguyên liệu
        </Button>
      </PageHead>
      <div className="overview-banner">
        <div>
          <span className="eyebrow">VIỆC CẦN XỬ LÝ</span>
          <h2>{pending.length} lô hàng đang chờ nhận hoặc KCS</h2>
          <p>Hoàn tất kiểm tra chất lượng để chuyển sang quyết toán.</p>
        </div>
        <Button secondary onClick={() => navigate("qc")}>
          Đến trạm cân →
        </Button>
      </div>
      <div className="section-toolbar">
        <h2>Hiệu quả thu mua</h2>
        <select
          aria-label="Kỳ thống kê"
          value={period}
          onChange={(e) => setPeriod(e.target.value)}
        >
          <option value="day">Hôm nay</option>
          <option value="month">Tháng này</option>
          <option value="year">Năm nay</option>
        </select>
      </div>
      <div className="kpi-grid">
        {[
          [
            "Khối lượng đã mua",
            `${number(weight / 1000)} tấn`,
            "Theo đơn đã quyết toán",
          ],
          ["Thực trả cho vựa", money(spend), "Sau khi trừ phí nền tảng"],
          [
            "Phí nền tảng tích lũy",
            money(fees),
            "5% tiền hàng • Chưa đối soát",
          ],
          ["Chờ quyết toán", `${ready.length} đơn`, "Đã nghiệm thu chất lượng"],
        ].map(([label, value, hint], i) => (
          <div className="kpi" key={label}>
            <div className="kpi-label">
              <span>{label}</span>
              <span className="kpi-icon">{["↙", "₫", "▤", "◷"][i]}</span>
            </div>
            <strong>{value}</strong>
            <small>{hint}</small>
          </div>
        ))}
      </div>
      <div className="two-columns">
        <Card
          title="Cơ cấu nguyên liệu đã mua"
          action={<span className="muted">Theo kỳ đã chọn</span>}
        >
          {volume.length ? (
            volume.map((v) => (
              <div className="bar-row" key={v.material}>
                <div>
                  <strong>{materials[v.material]}</strong>
                  <span>{number(v.kg)} kg</span>
                </div>
                <div className="bar-track">
                  <span style={{ width: `${(v.kg / max) * 100}%` }} />
                </div>
              </div>
            ))
          ) : (
            <Empty text="Chưa có giao dịch trong kỳ" />
          )}
          <div className="summary-line">
            Giá mua trung bình trước phí{" "}
            <strong>
              {money(
                weight
                  ? paid.reduce((n, o) => n + o.payment.total, 0) / weight
                  : 0,
              )}
              /kg
            </strong>
          </div>
        </Card>
        <Card title="Thực trả trong 6 tháng gần nhất">
          <div className="trend-chart">
            {trend.map((t, i) => (
              <div className="trend-col" key={i}>
                <small>{money(t.value)}</small>
                <div className="trend-track">
                  <div
                    style={{
                      height: `${(t.value / Math.max(1, ...trend.map((x) => x.value))) * 100}%`,
                    }}
                  />
                </div>
                <span>{t.label}</span>
              </div>
            ))}
          </div>
          <p className="muted">
            Tổng số tiền xác nhận trả cho vựa, sau phí nền tảng.
          </p>
        </Card>
      </div>
      <Card
        title="Ưu tiên xử lý"
        action={
          <Button secondary onClick={() => navigate("orders")}>
            Tất cả đơn hàng →
          </Button>
        }
      >
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Đơn / Vật liệu</th>
                <th>Vựa cung cấp</th>
                <th>Khối lượng khai báo</th>
                <th>Trạng thái</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {[...pending, ...ready].map((o) => (
                <tr key={o.id}>
                  <td>
                    <strong>{o.id}</strong>
                    <small>{materials[o.material]}</small>
                  </td>
                  <td>{state.depots.find((d) => d.id === o.depotId)?.name}</td>
                  <td>{number(o.kg)} kg</td>
                  <td>
                    <Status value={o.status} />
                  </td>
                  <td>
                    <Button
                      secondary
                      onClick={() =>
                        navigate(
                          o.status === "VERIFIED" ? "settlements" : "qc",
                          o.id,
                        )
                      }
                    >
                      Xử lý →
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {pending.length + ready.length === 0 && (
            <Empty text="Không có đơn cần xử lý" />
          )}
        </div>
      </Card>
    </>
  );
}
export function Marketplace({ navigate }) {
  const { state, act } = useFactory();
  const [filters, setFilters] = useState({
    search: "",
    material: "",
    min: "",
    max: "",
    distance: "",
    direct: false,
  });
  const [selected, setSelected] = useState(null);
  const [reason, setReason] = useState("");
  const [rejecting, setRejecting] = useState(false);
  const set = (key, value) => setFilters((f) => ({ ...f, [key]: value }));
  const batches = state.batches.filter((b) => {
    const d = state.depots.find((d) => d.id === b.depotId);
    return (
      b.status === "LISTED" &&
      b.direct === filters.direct &&
      d.status !== "BLOCKED" &&
      (!filters.material || b.material === filters.material) &&
      `${b.id} ${materials[b.material]} ${d.name}`
        .toLowerCase()
        .includes(filters.search.toLowerCase()) &&
      (!filters.min || b.kg >= Number(filters.min)) &&
      (!filters.max || b.kg <= Number(filters.max)) &&
      (!filters.distance || d.distance <= Number(filters.distance))
    );
  });
  const depot = selected && state.depots.find((d) => d.id === selected.depotId);
  return (
    <>
      <PageHead
        title="Sàn nguyên liệu"
        text="Nhận nguồn hàng phù hợp. Đơn giá được thỏa thuận sau khi cân và kiểm tra chất lượng."
      />
      <div className="tabs">
        <button
          className={!filters.direct ? "active" : ""}
          onClick={() => set("direct", false)}
        >
          Lô công khai
        </button>
        <button
          className={filters.direct ? "active" : ""}
          onClick={() => set("direct", true)}
        >
          Yêu cầu chỉ định
        </button>
      </div>
      {filters.direct && (
        <div className="info-box">
          Yêu cầu của vựa mới cần được duyệt. Vựa đã được duyệt được chuyển
          thẳng sang đơn hàng; vựa bị chặn không được chỉ định.
        </div>
      )}
      <div className="filter-bar">
        <Field
          label="Tìm kiếm"
          placeholder="Mã lô, tên vựa…"
          value={filters.search}
          onChange={(e) => set("search", e.target.value)}
        />
        <Field label="Vật liệu">
          <MaterialSelect
            all
            value={filters.material}
            onChange={(e) => set("material", e.target.value)}
          />
        </Field>
        <Field
          label="Từ (kg)"
          type="number"
          min="0"
          value={filters.min}
          onChange={(e) => set("min", e.target.value)}
        />
        <Field
          label="Đến (kg)"
          type="number"
          min="0"
          value={filters.max}
          onChange={(e) => set("max", e.target.value)}
        />
        <Field
          label="Khoảng cách tối đa (km)"
          type="number"
          min="0"
          value={filters.distance}
          onChange={(e) => set("distance", e.target.value)}
        />
      </div>
      <div className="section-toolbar">
        <span className="muted">
          {batches.length} lô khả dụng · Khoảng cách minh họa
        </span>
        <Button
          secondary
          onClick={() =>
            setFilters({
              search: "",
              material: "",
              min: "",
              max: "",
              distance: "",
              direct: filters.direct,
            })
          }
        >
          Xóa bộ lọc
        </Button>
      </div>
      <div className="batch-grid">
        {batches.map((b) => {
          const d = state.depots.find((d) => d.id === b.depotId);
          return (
            <article className="batch-card" key={b.id}>
              <div className={`material-cover material-${b.material}`}>
                <span>{b.material}</span>
                <span className="material-symbol">
                  {b.material === "PET"
                    ? "♻"
                    : b.material === "IRON"
                      ? "▰"
                      : "▱"}
                </span>
                <small>NGUYÊN LIỆU TÁI CHẾ</small>
              </div>
              <div className="batch-body">
                <div className="batch-meta">
                  <span>{b.id}</span>
                  <span>{d.distance} km</span>
                </div>
                <h2>{materials[b.material]}</h2>
                <p>{d.name}</p>
                <div className="batch-facts">
                  <div>
                    <small>Khối lượng dự kiến</small>
                    <strong>{number(b.kg / 1000)} tấn</strong>
                  </div>
                  <div>
                    <small>Đơn giá</small>
                    <strong>Sau KCS</strong>
                  </div>
                </div>
                <p className="muted">Vựa chịu phí vận chuyển đến nhà máy.</p>
                <Button
                  secondary
                  onClick={() => {
                    setSelected(b);
                    setRejecting(false);
                    setReason("");
                  }}
                >
                  Xem lô & {b.direct ? "duyệt yêu cầu" : "nhận hàng"} →
                </Button>
              </div>
            </article>
          );
        })}
      </div>
      {!batches.length && (
        <Card>
          <Empty text="Không có lô phù hợp" />
        </Card>
      )}
      {selected && (
        <Modal
          title={`Chi tiết ${selected.id}`}
          onClose={() => setSelected(null)}
        >
          <h3>
            {materials[selected.material]} · {number(selected.kg)} kg
          </h3>
          <p>
            <strong>{depot.name}</strong>
            <br />
            {depot.address}
            <br />
            {depot.phone}
          </p>
          <Status value={depot.status} />
          <p>{selected.note}</p>
          <div className="info-box">
            Nhận lô sẽ tạo đơn chờ vận chuyển. Chưa chốt giá và chưa phát sinh
            thanh toán.
          </div>
          {rejecting && (
            <Field label="Lý do từ chối">
              <textarea
                required
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </Field>
          )}
          <div className="form-actions">
            <Button secondary onClick={() => setSelected(null)}>
              Đóng
            </Button>
            {selected.direct && (
              <Button
                danger
                onClick={() => {
                  if (!rejecting) setRejecting(true);
                  else if (
                    act(
                      "REJECT_OFFER",
                      { id: selected.id, reason },
                      "Đã từ chối yêu cầu chỉ định.",
                    )
                  )
                    setSelected(null);
                }}
              >
                {rejecting ? "Xác nhận từ chối" : "Từ chối"}
              </Button>
            )}
            <Button
              onClick={() => {
                if (
                  act(
                    "ACCEPT_BATCH",
                    { id: selected.id },
                    "Đã nhận lô. Đơn hàng đang chờ tài xế.",
                  )
                ) {
                  setSelected(null);
                  navigate("orders");
                }
              }}
            >
              Nhận lô hàng
            </Button>
          </div>
        </Modal>
      )}
    </>
  );
}
export function Demands() {
  const { state, act } = useFactory();
  const [editing, setEditing] = useState(null);
  const [remove, setRemove] = useState(null);
  const [filter, setFilter] = useState("");
  return (
    <>
      <PageHead
        title="Nhu cầu thu mua"
        text="Chia sẻ kế hoạch nguyên liệu để vựa chuẩn bị nguồn cung phù hợp."
      >
        <Button
          onClick={() =>
            setEditing({
              material: "PET",
              kg: "",
              minPrice: "",
              maxPrice: "",
              deadline: "",
              note: "",
              active: true,
            })
          }
        >
          + Đăng nhu cầu
        </Button>
      </PageHead>
      <div className="filter-bar">
        <Field label="Vật liệu">
          <MaterialSelect
            all
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          />
        </Field>
      </div>
      <Card title="Kế hoạch đang đăng">
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Nhu cầu</th>
                <th>Khối lượng</th>
                <th>Khoảng giá / kg</th>
                <th>Hạn nhận</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {state.demands
                .filter((d) => !filter || d.material === filter)
                .map((d) => (
                  <tr key={d.id}>
                    <td>
                      <strong>{materials[d.material]}</strong>
                      <small>{d.id}</small>
                      <small>{d.note}</small>
                    </td>
                    <td>{number(d.kg)} kg</td>
                    <td>
                      {money(d.minPrice)} – {money(d.maxPrice)}
                    </td>
                    <td>{d.deadline}</td>
                    <td>
                      <span className="status">
                        {d.deadline < today()
                          ? "Hết hạn"
                          : d.active
                            ? "Đang mở"
                            : "Tạm dừng"}
                      </span>
                    </td>
                    <td>
                      <div className="actions">
                        <Button secondary onClick={() => setEditing(d)}>
                          Sửa
                        </Button>
                        <Button
                          secondary
                          onClick={() => act("TOGGLE_DEMAND", { id: d.id })}
                        >
                          {d.active ? "Tạm dừng" : "Mở lại"}
                        </Button>
                        <Button danger onClick={() => setRemove(d)}>
                          Xóa
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
          {!state.demands.some((d) => !filter || d.material === filter) && (
            <Empty />
          )}
        </div>
      </Card>
      {editing && (
        <Modal
          title={editing.id ? "Chỉnh sửa nhu cầu" : "Đăng nhu cầu thu mua"}
          onClose={() => setEditing(null)}
        >
          <form
            onSubmit={(e) => {
              e.preventDefault();
              if (act("SAVE_DEMAND", editing)) setEditing(null);
            }}
          >
            <div className="form-grid">
              <Field label="Vật liệu">
                <MaterialSelect
                  value={editing.material}
                  onChange={(e) =>
                    setEditing({ ...editing, material: e.target.value })
                  }
                />
              </Field>
              {[
                ["kg", "Khối lượng (kg)", "number"],
                ["minPrice", "Giá từ (đ/kg)", "number"],
                ["maxPrice", "Giá đến (đ/kg)", "number"],
                ["deadline", "Hạn nhận hàng", "date"],
              ].map(([key, label, type]) => (
                <Field
                  key={key}
                  label={label}
                  required
                  type={type}
                  min={type === "date" ? today() : key === "kg" ? 0.01 : 0}
                  step={type === "number" ? "any" : undefined}
                  value={editing[key]}
                  onChange={(e) =>
                    setEditing({ ...editing, [key]: e.target.value })
                  }
                />
              ))}
              <Field label="Yêu cầu chất lượng">
                <textarea
                  value={editing.note}
                  onChange={(e) =>
                    setEditing({ ...editing, note: e.target.value })
                  }
                />
              </Field>
            </div>
            <div className="form-actions">
              <Button secondary onClick={() => setEditing(null)}>
                Hủy
              </Button>
              <Button type="submit">Lưu nhu cầu</Button>
            </div>
          </form>
        </Modal>
      )}
      {remove && (
        <Confirm
          title="Xóa nhu cầu?"
          text={`Tin ${remove.id} sẽ được gỡ khỏi bảng nhu cầu.`}
          danger
          onClose={() => setRemove(null)}
          onConfirm={() => {
            if (act("DELETE_DEMAND", { id: remove.id })) setRemove(null);
          }}
        />
      )}
    </>
  );
}
export function Partners({ navigate }) {
  const { state, act } = useFactory();
  const [filter, setFilter] = useState("");
  const [change, setChange] = useState(null);
  return (
    <>
      <PageHead
        title="Vựa đối tác"
        text="Quản lý quan hệ hợp tác. Điểm đánh giá được ghi nhận theo từng đơn đã quyết toán."
      />
      <div className="filter-bar">
        <Field label="Trạng thái">
          <select value={filter} onChange={(e) => setFilter(e.target.value)}>
            <option value="">Tất cả</option>
            {["PENDING", "APPROVED", "BLOCKED"].map((s) => (
              <option key={s} value={s}>
                {
                  {
                    PENDING: "Chờ duyệt",
                    APPROVED: "Đã duyệt",
                    BLOCKED: "Đã chặn",
                  }[s]
                }
              </option>
            ))}
          </select>
        </Field>
      </div>
      <div className="partner-grid">
        {state.depots
          .filter((d) => !filter || d.status === filter)
          .map((d) => {
            const orders = state.orders.filter(
              (o) => o.depotId === d.id && o.status === "PAID",
            );
            const ratings = orders.filter((o) => o.rating);
            return (
              <Card
                key={d.id}
                title={d.name}
                action={<Status value={d.status} />}
              >
                <p>
                  {d.address}
                  <br />
                  {d.phone}
                </p>
                <div className="batch-facts">
                  <div>
                    <small>Đã quyết toán</small>
                    <strong>{orders.length} lô</strong>
                  </div>
                  <div>
                    <small>Đánh giá theo đơn</small>
                    <strong>
                      {ratings.length
                        ? `★ ${number(ratings.reduce((n, o) => n + o.rating.stars, 0) / ratings.length)}/5`
                        : "Chưa có"}
                    </strong>
                  </div>
                </div>
                <div className="actions">
                  <Button
                    secondary
                    onClick={() =>
                      navigate(
                        "settlements",
                        orders.find((o) => !o.rating)?.id || orders[0]?.id,
                      )
                    }
                  >
                    Xem đơn & đánh giá
                  </Button>
                  <Button
                    secondary={d.status === "BLOCKED"}
                    danger={d.status !== "BLOCKED"}
                    onClick={() => setChange(d)}
                  >
                    {d.status === "BLOCKED" ? "Bỏ chặn" : "Chặn vựa"}
                  </Button>
                </div>
                {ratings.map((o) => (
                  <p className="review" key={o.id}>
                    <strong>
                      {o.id} · {o.rating.stars}/5
                    </strong>
                    <br />
                    {o.rating.comment || "Không có nhận xét."}
                  </p>
                ))}
              </Card>
            );
          })}
      </div>
      {!state.depots.some((d) => !filter || d.status === filter) && <Empty />}
      {change && (
        <Confirm
          title={change.status === "BLOCKED" ? "Bỏ chặn vựa?" : "Chặn vựa?"}
          text={`${change.name}: ${change.status === "BLOCKED" ? "cho phép hợp tác và chỉ định lô trở lại." : "ngừng nhận lô mới từ vựa. Các đơn hiện có vẫn cần được xử lý."}`}
          danger={change.status !== "BLOCKED"}
          onClose={() => setChange(null)}
          onConfirm={() => {
            if (
              act("PARTNER_STATUS", {
                id: change.id,
                status: change.status === "BLOCKED" ? "APPROVED" : "BLOCKED",
              })
            )
              setChange(null);
          }}
        />
      )}
    </>
  );
}
export function Profile() {
  const { state, act } = useFactory();
  const [form, setForm] = useState(state.profile);
  const set = (key, value) => setForm((f) => ({ ...f, [key]: value }));
  return (
    <>
      <PageHead
        title="Hồ sơ nhà máy"
        text="Thông tin tiếp nhận nguyên liệu và giấy tờ phục vụ hợp tác với kho vựa."
      />
      <form
        onSubmit={(e) => {
          e.preventDefault();
          act("PROFILE", form, "Đã lưu hồ sơ nhà máy.");
        }}
      >
        <Card title="Thông tin doanh nghiệp">
          <div className="form-grid">
            {[
              ["companyName", "Tên nhà máy"],
              ["taxCode", "Mã số thuế"],
              ["address", "Địa chỉ tiếp nhận"],
              ["industrialZone", "Khu công nghiệp"],
              ["phone", "Điện thoại"],
              ["capacity", "Công suất (kg/tháng)", "number"],
              ["latitude", "Vĩ độ", "number"],
              ["longitude", "Kinh độ", "number"],
              ["purity", "Độ tinh khiết tối thiểu (%)", "number"],
            ].map(([key, label, type]) => (
              <Field
                key={key}
                label={label}
                required={key !== "industrialZone"}
                type={type || "text"}
                step={type === "number" ? "any" : undefined}
                value={form[key]}
                onChange={(e) => set(key, e.target.value)}
              />
            ))}
          </div>
        </Card>
        <Card title="Vật liệu chấp nhận">
          <div className="checkbox-grid">
            {Object.entries(materials).map(([key, label]) => (
              <label key={key}>
                <input
                  type="checkbox"
                  checked={form.materials.includes(key)}
                  onChange={(e) =>
                    set(
                      "materials",
                      e.target.checked
                        ? [...form.materials, key]
                        : form.materials.filter((m) => m !== key),
                    )
                  }
                />
                {label}
              </label>
            ))}
          </div>
          <Field label="Vật liệu chính">
            <select
              value={form.primaryMaterial}
              onChange={(e) => set("primaryMaterial", e.target.value)}
            >
              {Object.entries(materials).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
          </Field>
        </Card>
        <Card title="Giấy phép & Hồ sơ pháp lý">
          <div className="form-grid">
            <Attachment
              label="Giấy phép kinh doanh"
              value={form.businessLicense}
              onChange={(f) => set("businessLicense", f)}
            />
            <Attachment
              label="Giấy phép môi trường"
              value={form.environmentLicense}
              onChange={(f) => set("environmentLicense", f)}
            />
          </div>
          <p className="muted">
            Tệp được lưu cùng hồ sơ trong trình duyệt ở phiên bản demo.
          </p>
        </Card>
        <div className="form-actions">
          <Button secondary onClick={() => setForm(state.profile)}>
            Bỏ thay đổi
          </Button>
          <Button type="submit">Lưu hồ sơ nhà máy</Button>
        </div>
      </form>
    </>
  );
}
export function Prices() {
  const { state } = useFactory();
  return (
    <>
      <PageHead
        title="Giá nguyên liệu tham khảo"
        text="Dùng để lập kế hoạch thu mua. Giá giao dịch cuối cùng được thỏa thuận sau KCS."
      />
      <Card title="Bảng giá minh họa">
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Vật liệu</th>
                <th>Giá tham khảo / kg</th>
                <th>Ngày dữ liệu</th>
                <th>Nguồn</th>
              </tr>
            </thead>
            <tbody>
              {state.prices.map((p) => (
                <tr key={p.material}>
                  <td>
                    <strong>{materials[p.material]}</strong>
                  </td>
                  <td>{money(p.price)}</td>
                  <td>{p.date}</td>
                  <td>{p.source}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </>
  );
}

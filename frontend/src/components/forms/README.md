# Báo lỗi biểu mẫu dùng chung

`ValidatedForm.jsx` dành cho React web, không phải React Native. Dùng `Form`, `Input`, `Select`, `Textarea` thay các thẻ HTML tương ứng trong biểu mẫu. Giữ nguyên API, state và handler nghiệp vụ của role.

```jsx
import { Form, Input } from '@/components/forms/ValidatedForm';

<Form onSubmit={event => { event.preventDefault(); save.mutate(form); }}>
  <label>Email<Input name="email" required type="email" value={form.email}
    onChange={event => setForm({ ...form, email: event.target.value })} /></label>
  <button disabled={save.isPending}>Lưu</button>
</Form>
```

- Khai báo đúng `required`, `min`, `max`, `step`, `minLength`, `maxLength`, `pattern` theo DTO/service đang chạy; không tự đặt thêm quy tắc kinh doanh.
- Trường có nhiều dòng giống nhau nên thêm `data-field-label`, ví dụ `Số lượng (kg) — dòng 2`. Nhãn này phải chứa đúng nội dung nhãn nhìn thấy. ID được cấp tự động nếu chưa truyền `id`.
- Form dùng `noValidate` để thay bóng lỗi của browser bằng UI thống nhất, nhưng vẫn kiểm Constraint Validation API và giới hạn chuỗi khi gửi. Không hiện lỗi trống ngay lúc mở.
- Sau gửi sai có summary, focus và liên kết tới field; lỗi dưới field liên kết qua `aria-describedby`. Trường trong details được mở trước khi focus. Sửa lỗi không xóa dữ liệu; bỏ field động/khóa field thì không giữ lỗi không còn áp dụng.
- Checkbox xác nhận nghiệp vụ phải giữ guard hiện hành. Không xóa xác nhận tiền thật/chữ ký/quyền để làm form “gọn”.
- Lỗi server/mạng/quyền dùng thông báo API riêng. Component không đoán một lỗi chung thuộc field nào, không thay validation/quyền/transaction phía server.
- Không tạo form HTML lồng nhau. Nút footer ở ngoài form tiếp tục dùng `form={formId}`; `event.nativeEvent.submitter` vẫn được giữ cho nhiều hành động.

Quy tắc liên field: truyền `validate(formData, formElement, submitter)` trả object `{ tenField: 'Cách sửa lỗi' }`; field cần `name` hoặc `id` trùng key và dùng control chung. Quy tắc riêng cho từng nút có thể đọc `submitter.value`. Handler nghiệp vụ vẫn giữ guard và chống gửi lặp của mình.

`data-pattern-message` giải thích định dạng thay cho câu chung; `data-validation-error` nối kết quả kiểm tệp có sẵn vào field. `data-validation-skip` chỉ dành cho input công cụ phụ không được gửi trong nghiệp vụ chính (ví dụ máy tính dự kiến); công cụ đó phải tự kiểm giới hạn trước gọi API. Không dùng để bỏ qua field bắt buộc.

Các form Auth/Seller/Money đang có bộ lỗi riêng cần tích hợp theo từng màn và giữ kiểm soát hiện có; không chỉ thay thẻ rồi coi là đã nghiệm thu toàn bộ.

`npm run lint` còn kiểm tên component JSX chưa khai báo/import bằng TypeScript đang có trong dự án. Đây là kiểm tra binding JSX trên toàn `src`, không phải chứng nhận toàn bộ dự án đã đạt strict typecheck. Vite build thành công riêng chưa đủ bắt lỗi tên component chưa import.

Tham khảo [W3C form notifications](https://www.w3.org/WAI/tutorials/forms/notifications/) và [GOV.UK error summary](https://design-system.service.gov.uk/components/error-summary/). Đã kiểm DOM/focus/keyboard và responsive ở các form đại diện; chưa chứng nhận toàn sản phẩm đạt WCAG bằng trình đọc màn hình/axe.

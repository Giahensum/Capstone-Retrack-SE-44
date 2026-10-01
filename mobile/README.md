# ReTrack Nhân sự

Chọn tuyến **Cân bằng / Ít km nhất / Nhanh nhất dự kiến** và phương tiện **Ô tô / Xe máy**: xem [hướng dẫn cập nhật app và kiểm thử tuyến đường trên điện thoại thật](./ROUTING_TEST_GUIDE.md). Bản cập nhật JavaScript này dùng app ReTrack đã cài cùng Metro, không cần build lại APK.

Test trên điện thoại thật: [Sửa kết nối Expo CLI và bản đồ Google Maps trống](../docs/ANDROID_REAL_DEVICE_TROUBLESHOOTING.md). Sau khi đổi key Maps, chạy `npm run android:prepare` rồi build/cài lại APK.

Nhánh `feature/emp-pickup-pool`: xem [hướng dẫn Dashboard, đơn chờ, nhận đơn và bản đồ](../docs/EMP_PICKUP_POOL_GUIDE.md). Employee hiện có 4 tab **Trang chủ · Đơn chờ · Lịch sử · Cá nhân**; cần cấu hình key Google Maps/Goong và build lại Android sau khi cài dependency mới.

App Android cho Depot Employee và Driver, dùng **JavaScript/JSX + React Navigation Stack/Bottom Tabs**. Entry: `App.jsx`. Không sử dụng Expo Router.

Hướng dẫn chi tiết: [Chạy và kiểm thử auth/profile](../docs/EMP_AUTH_PROFILE_IMPLEMENTATION.md), bao gồm PostgreSQL, backend, tài khoản mẫu, Expo Go, Android Studio, Cloudinary và xử lý lỗi.

## Chạy nhanh

Terminal A từ root repository:

```powershell
dotnet run --project backend/Retrack.API --launch-profile http
```

Terminal B từ `mobile`:

```powershell
npm ci
Copy-Item .env.example .env
npx expo start --go --clear
```

Chỉ sao chép `.env.example` nếu chưa có `.env` riêng. Mở AVD trong Android Studio, nhấn `a` ở terminal Expo. API mặc định: `http://10.0.2.2:5000/api`.

Test native build:

```powershell
npx expo prebuild --platform android --no-install
npm run android
```

Hoặc mở thư mục `android` đã sinh trong Android Studio, Sync Gradle và Run app; giữ Metro cùng backend đang chạy. `android/` được sinh từ config, không chỉnh tay hoặc commit.

## Kiểm tra

```powershell
npm run typecheck
npm run lint
npm test
npm run export:android
```

`typecheck` kiểm tra JavaScript bằng checkJs, không chuyển source về TypeScript. Export Android kiểm tra bundle, không tạo APK.

## Cấu trúc

- `src/navigation/`: LoginStack, EmployeeNavigator, DriverNavigator, ProfileNavigator.
- `src/screens/`: màn hình `.jsx` theo role; Hồ sơ đầy đủ, các tab nghiệp vụ khác là placeholder.
- `src/components/common/`: Button, Input, LoadingSpinner và thành phần UI dùng chung.
- `src/api/`: Axios, auth/profile, notification API chuẩn bị cho nhánh tiếp theo, SecureStore.
- `src/store/`: Zustand JavaScript và logic trạng thái có unit test.
- `src/helpers/`: validation, chọn/cắt ảnh; `src/theme.js`: màu sắc và style.

Employee dùng 4 tab **Trang chủ · Đơn chờ · Lịch sử · Cá nhân**. Dashboard và đơn chờ gọi API thật; Lịch sử là placeholder. Driver giữ 5 tab, chọn **Hồ sơ** để test auth/profile. Các tab “sắp ra mắt” không gọi API.

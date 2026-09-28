# ReTrack Nhân sự

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

Sau đăng nhập chọn tab **Hồ sơ** để test. Hai role có 5 tab riêng; các tab “sắp ra mắt” không gọi API.

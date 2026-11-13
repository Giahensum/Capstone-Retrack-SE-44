import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';
import { Loader2 } from 'lucide-react';

export default function SellerProfile() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({
    fullName: '',
    phone: '',
    bankName: '',
    bankAccountNumber: '',
    bankAccountName: '',
    bankQrUrl: '',
  });
  const [uploadingQr, setUploadingQr] = useState(false);

  const { data: profile, isLoading } = useQuery({
    queryKey: ['seller-profile'],
    queryFn: sellerApi.getProfile,
    onSuccess: (data) =>
      setForm({
        fullName: data.fullName || '',
        phone: data.phone || '',
        bankName: data.bankName || '',
        bankAccountNumber: data.bankAccountNumber || '',
        bankAccountName: data.bankAccountName || '',
        bankQrUrl: data.bankQrUrl || '',
      }),
  });

  const updateMutation = useMutation({
    mutationFn: sellerApi.updateProfile,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['seller-profile'] });
      toast.success('Cập nhật thông tin thành công!');
      setEditing(false);
    },
    onError: () => toast.error('Có lỗi xảy ra khi cập nhật'),
  });

  if (isLoading) {
    return (
      <div className="flex justify-center items-center h-64">
        <div className="w-8 h-8 border-3 border-d-primary border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  const initials = (profile?.fullName ?? 'S')
    .trim()
    .split(/\s+/)
    .slice(-2)
    .map((s) => s[0])
    .join('')
    .toLocaleUpperCase('vi-VN') || 'NB';

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-3xl mx-auto">
      {/* Title */}
      <div>
        <h1 className="font-d-headline-xl-mobile md:font-d-headline-xl text-d-headline-xl-mobile md:text-d-headline-xl text-d-on-surface">
          Hồ sơ cá nhân
        </h1>
        <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">
          Quản lý thông tin liên hệ và tài khoản ngân hàng nhận tiền thu gom phế liệu.
        </p>
      </div>

      {/* Avatar Banner Card */}
      <div className="relative overflow-hidden rounded-[20px] bg-gradient-to-r from-d-primary to-[#2a4400] text-d-on-primary p-6 md:p-8 text-center shadow-sm">
        <div className="absolute top-0 right-0 w-64 h-64 bg-white/5 rounded-full -translate-y-1/2 translate-x-1/2 blur-xl" />
        <div className="relative z-10 flex flex-col items-center">
          <div className="w-20 h-20 bg-d-primary-container text-d-on-primary-container rounded-full flex items-center justify-center mb-3 text-2xl font-bold shadow-md">
            {initials}
          </div>
          <h2 className="text-xl font-bold">{profile?.fullName}</h2>
          <p className="text-xs text-white/80 mt-0.5">{profile?.email}</p>
          <span className="inline-block mt-3 text-[11px] font-bold bg-white/15 text-d-primary-container px-3 py-1 rounded-full uppercase tracking-wider">
            Người bán phế liệu ReTrack
          </span>
        </div>
      </div>

      {/* Thông tin liên hệ */}
      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl shadow-sm overflow-hidden">
        <div className="flex items-center justify-between px-6 py-4 border-b border-d-border-subtle bg-d-surface-container-low">
          <h3 className="text-sm font-bold text-d-on-surface">Thông tin liên hệ</h3>
          {!editing ? (
            <button
              type="button"
              onClick={() => {
                setForm({
                  fullName: profile?.fullName || '',
                  phone: profile?.phone || '',
                  bankName: profile?.bankName || '',
                  bankAccountNumber: profile?.bankAccountNumber || '',
                  bankAccountName: profile?.bankAccountName || '',
                  bankQrUrl: profile?.bankQrUrl || '',
                });
                setEditing(true);
              }}
              className="inline-flex items-center gap-1.5 text-xs font-semibold text-d-primary hover:underline"
            >
              <MaterialIcon name="fact_check" className="text-sm" />
              <span>Chỉnh sửa thông tin</span>
            </button>
          ) : (
            <button
              type="button"
              onClick={() => setEditing(false)}
              className="text-xs font-semibold text-d-on-surface-variant hover:text-d-error"
            >
              ✕ Hủy bỏ
            </button>
          )}
        </div>

        {!editing ? (
          <div className="divide-y divide-d-border-subtle">
            <InfoRow
              icon={<MaterialIcon name="inventory_2" className="text-d-primary text-base" />}
              label="Họ và tên"
              value={profile?.fullName}
            />
            <InfoRow
              icon={<MaterialIcon name="notifications" className="text-d-primary text-base" />}
              label="Địa chỉ email"
              value={profile?.email}
            />
            <InfoRow
              icon={<MaterialIcon name="local_shipping" className="text-d-primary text-base" />}
              label="Số điện thoại"
              value={profile?.phone || '(Chưa cập nhật)'}
            />
          </div>
        ) : (
          <div className="p-6 space-y-4">
            <div>
              <label className="text-xs font-semibold text-d-on-surface mb-1.5 block">
                Họ và tên
              </label>
              <input
                value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                className="w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all"
              />
            </div>

            <div>
              <label className="text-xs font-semibold text-d-on-surface mb-1.5 block">
                Số điện thoại
              </label>
              <input
                value={form.phone}
                onChange={(e) => setForm({ ...form, phone: e.target.value })}
                placeholder="0909 xxx xxx"
                className="w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all"
              />
            </div>

            <div className="pt-2">
              <button
                type="button"
                onClick={() => updateMutation.mutate(form)}
                disabled={updateMutation.isPending}
                className="w-full rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary font-bold py-3 text-sm transition-all shadow-sm active:scale-95 disabled:opacity-50 flex items-center justify-center gap-2"
              >
                {updateMutation.isPending ? (
                  <Loader2 size={18} className="animate-spin" />
                ) : (
                  <span>Lưu thông tin liên hệ</span>
                )}
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Thông tin tài khoản ngân hàng */}
      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-d-border-subtle bg-d-surface-container-low flex justify-between items-center">
          <div>
            <h3 className="text-sm font-bold text-d-on-surface">
              Tài khoản ngân hàng nhận tiền
            </h3>
            <p className="text-xs text-d-on-surface-variant mt-0.5">
              Dùng để kho vựa chuyển tiền thanh toán tiền bán phế liệu
            </p>
          </div>
        </div>

        {!editing ? (
          <div className="divide-y divide-d-border-subtle">
            <InfoRow label="Ngân hàng" value={profile?.bankName || '(Chưa cập nhật)'} />
            <InfoRow label="Số tài khoản" value={profile?.bankAccountNumber || '(Chưa cập nhật)'} />
            <InfoRow label="Tên chủ tài khoản" value={profile?.bankAccountName || '(Chưa cập nhật)'} />
            {profile?.bankQrUrl && (
              <div className="p-6 flex flex-col items-center gap-2 bg-d-surface-container-low">
                <span className="text-xs font-semibold text-d-on-surface-variant">
                  Mã QR chuyển khoản cá nhân:
                </span>
                <img
                  src={profile.bankQrUrl}
                  alt="QR Code"
                  className="w-36 h-36 rounded-xl object-contain border border-d-border-subtle bg-white p-2 shadow-xs"
                />
              </div>
            )}
          </div>
        ) : (
          <div className="p-6 space-y-4">
            <div>
              <label className="text-xs font-semibold text-d-on-surface mb-1.5 block">
                Tên Ngân hàng
              </label>
              <input
                value={form.bankName}
                onChange={(e) => setForm({ ...form, bankName: e.target.value })}
                placeholder="VD: Vietcombank, MB Bank, Techcombank..."
                className="w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all"
              />
            </div>

            <div>
              <label className="text-xs font-semibold text-d-on-surface mb-1.5 block">
                Số tài khoản
              </label>
              <input
                value={form.bankAccountNumber}
                onChange={(e) => setForm({ ...form, bankAccountNumber: e.target.value })}
                placeholder="Nhập số tài khoản"
                className="w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all"
              />
            </div>

            <div>
              <label className="text-xs font-semibold text-d-on-surface mb-1.5 block">
                Tên chủ tài khoản (Viết in hoa không dấu)
              </label>
              <input
                value={form.bankAccountName}
                onChange={(e) => setForm({ ...form, bankAccountName: e.target.value })}
                placeholder="VD: NGUYEN VAN A"
                className="w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all"
              />
            </div>

            <div>
              <label className="text-xs font-semibold text-d-on-surface mb-1.5 block">
                Ảnh mã QR thanh toán (Tải lên Cloudinary)
              </label>
              {form.bankQrUrl ? (
                <div className="flex items-start gap-4">
                  <img
                    src={form.bankQrUrl}
                    alt="QR preview"
                    className="w-28 h-28 rounded-xl object-contain border border-d-border-subtle bg-white p-2"
                  />
                  <button
                    type="button"
                    onClick={() => setForm({ ...form, bankQrUrl: '' })}
                    className="text-xs text-d-error hover:underline font-semibold mt-1"
                  >
                    ✕ Xóa ảnh QR
                  </button>
                </div>
              ) : (
                <div className="flex items-center gap-3">
                  <label className="cursor-pointer inline-flex items-center gap-2 text-xs font-semibold text-d-primary bg-d-surface-container hover:bg-d-surface-container-high border border-d-border-subtle px-4 py-2.5 rounded-xl transition-all">
                    <MaterialIcon name="inventory_2" className="text-base" />
                    <span>Chọn ảnh mã QR từ máy</span>
                    <input
                      type="file"
                      accept="image/*"
                      className="hidden"
                      onChange={async (e) => {
                        if (e.target.files && e.target.files[0]) {
                          setUploadingQr(true);
                          try {
                            const url = await sellerApi.uploadImage(e.target.files[0]);
                            setForm({ ...form, bankQrUrl: url });
                            toast.success('Tải ảnh QR thành công');
                          } catch {
                            toast.error('Lỗi khi tải ảnh lên Cloudinary');
                          } finally {
                            setUploadingQr(false);
                          }
                        }
                      }}
                    />
                  </label>
                  {uploadingQr && (
                    <span className="text-xs text-d-on-surface-variant flex items-center gap-1">
                      <Loader2 size={14} className="animate-spin text-d-primary" />
                      <span>Đang tải ảnh lên...</span>
                    </span>
                  )}
                </div>
              )}
            </div>

            <div className="pt-2">
              <button
                type="button"
                onClick={() => updateMutation.mutate(form)}
                disabled={updateMutation.isPending}
                className="w-full rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary font-bold py-3 text-sm transition-all shadow-sm active:scale-95 disabled:opacity-50 flex items-center justify-center gap-2"
              >
                {updateMutation.isPending ? (
                  <Loader2 size={18} className="animate-spin" />
                ) : (
                  <span>Lưu thông tin ngân hàng</span>
                )}
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Thông tin trạng thái tài khoản */}
      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl shadow-sm overflow-hidden divide-y divide-d-border-subtle">
        <div className="px-6 py-4 bg-d-surface-container-low">
          <h3 className="text-sm font-bold text-d-on-surface">Trạng thái tài khoản</h3>
        </div>
        <InfoRow label="Vai trò hệ thống" value="Người bán (Seller)" />
        <InfoRow
          label="Tình trạng"
          value={profile?.isActive !== false ? '✅ Đang hoạt động' : '⛔ Đang bị khóa'}
        />
        <InfoRow
          label="Ngày tham gia"
          value={
            profile?.createdAt
              ? new Date(profile.createdAt).toLocaleDateString('vi-VN')
              : '–'
          }
        />
      </div>
    </div>
  );
}

function InfoRow({ icon, label, value }) {
  return (
    <div className="flex items-center justify-between px-6 py-4 text-sm">
      <div className="flex items-center gap-2.5">
        {icon}
        <span className="text-xs text-d-on-surface-variant font-medium">{label}</span>
      </div>
      <span className="font-semibold text-d-on-surface text-right">{value}</span>
    </div>
  );
}

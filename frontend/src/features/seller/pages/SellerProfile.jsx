import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import { User, Mail, Phone, Edit3, Save, X } from 'lucide-react';

export default function SellerProfile() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({ fullName: '', phone: '' });

  const { data: profile, isLoading } = useQuery({
    queryKey: ['seller-profile'],
    queryFn: sellerApi.getProfile,
    onSuccess: (data) => setForm({ fullName: data.fullName, phone: data.phone }),
  });

  const updateMutation = useMutation({
    mutationFn: sellerApi.updateProfile,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['seller-profile'] });
      toast.success('Cập nhật thành công!');
      setEditing(false);
    },
    onError: () => toast.error('Có lỗi xảy ra'),
  });

  if (isLoading) return (
    <div className="flex justify-center items-center h-64">
      <div className="w-8 h-8 border-3 border-emerald-500 border-t-transparent rounded-full animate-spin" />
    </div>
  );

  const initials = (profile?.fullName ?? 'S').split(' ').filter(Boolean).slice(-2).map(s => s[0]).join('').toUpperCase();

  return (
    <div className="p-4 lg:p-8 max-w-2xl mx-auto space-y-6">
      <h1 className="text-xl font-black text-white">Hồ sơ cá nhân</h1>

      {/* Avatar card */}
      <div className="bg-gradient-to-br from-emerald-600 via-emerald-500 to-teal-500 rounded-2xl p-6 text-center relative overflow-hidden">
        <div className="absolute top-0 right-0 w-40 h-40 bg-white/5 rounded-full -translate-y-1/2 translate-x-1/2" />
        <div className="w-20 h-20 bg-white/20 backdrop-blur-sm rounded-full flex items-center justify-center mx-auto mb-3 text-2xl font-black text-white border-2 border-white/30">
          {initials}
        </div>
        <h2 className="text-lg font-bold text-white">{profile?.fullName}</h2>
        <p className="text-sm text-emerald-100">{profile?.email}</p>
        <span className="inline-block mt-2 text-[10px] font-bold bg-white/20 text-white px-3 py-1 rounded-full uppercase tracking-wider">
          Người bán phế liệu
        </span>
      </div>

      {/* Info */}
      <div className="bg-slate-800/50 border border-slate-700/50 rounded-2xl overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-slate-700/50">
          <h3 className="text-sm font-bold text-slate-200">Thông tin cá nhân</h3>
          {!editing ? (
            <button onClick={() => { setForm({ fullName: profile?.fullName || '', phone: profile?.phone || '' }); setEditing(true); }} className="flex items-center gap-1.5 text-xs text-emerald-400 font-semibold hover:text-emerald-300">
              <Edit3 size={13} /> Chỉnh sửa
            </button>
          ) : (
            <button onClick={() => setEditing(false)} className="flex items-center gap-1.5 text-xs text-slate-400 font-semibold hover:text-slate-300">
              <X size={13} /> Hủy
            </button>
          )}
        </div>

        {!editing ? (
          <div className="divide-y divide-slate-700/50">
            <InfoRow icon={<User size={14} className="text-blue-400" />} label="Họ tên" value={profile?.fullName} />
            <InfoRow icon={<Mail size={14} className="text-purple-400" />} label="Email" value={profile?.email} />
            <InfoRow icon={<Phone size={14} className="text-emerald-400" />} label="Số điện thoại" value={profile?.phone || '(chưa cập nhật)'} />
          </div>
        ) : (
          <div className="p-5 space-y-4">
            <div>
              <label className="text-xs font-semibold text-slate-400 mb-1.5 block">Họ tên</label>
              <input
                value={form.fullName}
                onChange={e => setForm({...form, fullName: e.target.value})}
                className="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-3 text-sm text-slate-200 focus:border-emerald-500 outline-none"
              />
            </div>
            <div>
              <label className="text-xs font-semibold text-slate-400 mb-1.5 block">Số điện thoại</label>
              <input
                value={form.phone}
                onChange={e => setForm({...form, phone: e.target.value})}
                placeholder="0909 xxx xxx"
                className="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-3 text-sm text-slate-200 placeholder-slate-600 focus:border-emerald-500 outline-none"
              />
            </div>
            <button
              onClick={() => updateMutation.mutate(form)}
              disabled={updateMutation.isPending}
              className="w-full bg-emerald-500 hover:bg-emerald-600 text-white font-bold py-3 rounded-xl flex items-center justify-center gap-2 transition-all active:scale-95 disabled:opacity-60"
            >
              <Save size={16} /> Lưu thay đổi
            </button>
          </div>
        )}
      </div>

      {/* Account info */}
      <div className="bg-slate-800/50 border border-slate-700/50 rounded-2xl divide-y divide-slate-700/50">
        <div className="px-5 py-4 border-b border-slate-700/50">
          <h3 className="text-sm font-bold text-slate-200">Thông tin tài khoản</h3>
        </div>
        <InfoRow label="Vai trò" value="Người bán (Seller)" />
        <InfoRow label="Trạng thái" value={profile?.isActive ? '✅ Hoạt động' : '⛔ Bị khóa'} />
        <InfoRow label="Ngày tham gia" value={profile?.createdAt ? new Date(profile.createdAt).toLocaleDateString('vi-VN') : '–'} />
      </div>
    </div>
  );
}

function InfoRow({ icon, label, value }) {
  return (
    <div className="flex items-center justify-between px-5 py-3.5">
      <div className="flex items-center gap-2">
        {icon}
        <span className="text-xs text-slate-500 font-medium">{label}</span>
      </div>
      <span className="text-sm text-slate-200 font-medium">{value}</span>
    </div>
  );
}

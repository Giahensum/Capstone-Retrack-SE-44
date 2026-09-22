import React, { useState } from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';
import EditProfileModal from './components/EditProfileModal';

const Profile = () => {
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);

  return (
    <div className="flex-1 w-full overflow-y-auto bg-d-surface relative h-full">
      <EditProfileModal isOpen={isEditModalOpen} onClose={() => setIsEditModalOpen(false)} />
      {/* Hero Section */}
      <section className="relative w-full h-[240px] md:h-[300px] bg-gradient-to-r from-[#1b3b22] to-d-primary overflow-hidden shrink-0">
        <div className="absolute inset-0 d-pattern-overlay opacity-30"></div>
      </section>

      {/* Profile Overlap & Content Grid */}
      <div className="max-w-container-max mx-auto px-4 md:px-10 -mt-24 pb-12 relative z-10">
        {/* Profile Header Card */}
        <div className="d-profile-glass rounded-2xl p-6 md:p-8 shadow-[0_20px_40px_rgba(23,33,27,0.04)] mb-8 flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
          <div className="flex flex-col md:flex-row items-start md:items-center gap-6 w-full">
            <div className="w-24 h-24 md:w-32 md:h-32 rounded-2xl bg-d-surface-container-lowest border-4 border-d-surface-container-lowest shadow-sm flex items-center justify-center flex-shrink-0 relative overflow-hidden group">
              <MaterialIcon name="warehouse" className="text-[64px] text-d-primary" style={{ fontVariationSettings: "'FILL' 1" }} />
              <div className="absolute inset-0 bg-black/50 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity cursor-pointer backdrop-blur-sm">
                <MaterialIcon name="photo_camera" className="text-white text-[24px]" />
              </div>
            </div>
            
            <div className="flex-1 space-y-2">
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="font-d-headline-lg text-d-headline-lg font-bold text-d-on-surface">Kho Vựa Phế Liệu Tân Bình</h1>
                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-d-surface-accent text-d-primary font-d-label-sm text-d-label-sm">
                  <span className="w-2 h-2 rounded-full bg-d-primary animate-pulse"></span>
                  Đang hoạt động
                </span>
              </div>
              <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-d-on-surface-variant font-d-body-sm text-d-body-sm">
                <div className="flex items-center gap-1.5">
                  <MaterialIcon name="star" className="text-[#F59E0B] text-[18px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                  <span className="font-semibold text-d-on-surface">4.5</span>
                  <span>(128 đánh giá)</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <MaterialIcon name="location_on" className="text-[18px]" />
                  <span>123 Đường Cộng Hòa, Phường 13, Tân Bình, TP.HCM</span>
                </div>
              </div>
            </div>
          </div>
          
          <button 
            onClick={() => setIsEditModalOpen(true)}
            className="w-full md:w-auto px-6 py-2.5 rounded-full border border-d-outline text-d-on-surface font-d-label-md text-d-label-md hover:bg-d-surface-container transition-all flex items-center justify-center gap-2 flex-shrink-0"
          >
            <MaterialIcon name="edit" className="text-[20px]" />
            Chỉnh sửa hồ sơ
          </button>
        </div>

        {/* 2-Column Main Content */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 md:gap-8">
          
          {/* Left Column (Basic Info & Map) */}
          <div className="lg:col-span-7 flex flex-col gap-6 md:gap-8">
            {/* Thông tin cơ bản */}
            <div className="bg-d-surface-container-lowest rounded-[20px] p-6 md:p-8 border border-d-border-subtle shadow-sm">
              <h2 className="font-d-headline-md text-d-headline-md font-semibold text-d-on-surface mb-6 flex items-center gap-2">
                <MaterialIcon name="info" className="text-d-primary" />
                Thông tin cơ bản
              </h2>
              
              <div className="space-y-6">
                {/* Info Rows */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-2 pb-4 border-b border-d-border-subtle border-dashed">
                  <div className="text-d-on-surface-variant font-d-label-md text-d-label-md flex items-center gap-2">
                    <MaterialIcon name="person" className="text-[18px]" /> Chủ sở hữu
                  </div>
                  <div className="md:col-span-2 font-d-body-md text-d-body-md font-medium text-d-on-surface">Nguyễn Văn A</div>
                </div>
                
                <div className="grid grid-cols-1 md:grid-cols-3 gap-2 pb-4 border-b border-d-border-subtle border-dashed">
                  <div className="text-d-on-surface-variant font-d-label-md text-d-label-md flex items-center gap-2">
                    <MaterialIcon name="call" className="text-[18px]" /> Số điện thoại
                  </div>
                  <div className="md:col-span-2 font-d-body-md text-d-body-md font-medium text-d-on-surface">0901 234 567</div>
                </div>
                
                <div className="grid grid-cols-1 md:grid-cols-3 gap-2 pb-4 border-b border-d-border-subtle border-dashed">
                  <div className="text-d-on-surface-variant font-d-label-md text-d-label-md flex items-center gap-2">
                    <MaterialIcon name="mail" className="text-[18px]" /> Email
                  </div>
                  <div className="md:col-span-2 font-d-body-md text-d-body-md font-medium text-d-on-surface">contact@khoquatbanbinh.com</div>
                </div>
                
                <div className="grid grid-cols-1 md:grid-cols-3 gap-2 pb-4 border-b border-d-border-subtle border-dashed">
                  <div className="text-d-on-surface-variant font-d-label-md text-d-label-md flex items-center gap-2">
                    <MaterialIcon name="receipt_long" className="text-[18px]" /> Mã số thuế
                  </div>
                  <div className="md:col-span-2 font-d-body-md text-d-body-md font-medium text-d-on-surface">0312345678</div>
                </div>
                
                <div className="grid grid-cols-1 md:grid-cols-3 gap-2 pb-4 border-b border-d-border-subtle border-dashed">
                  <div className="text-d-on-surface-variant font-d-label-md text-d-label-md flex items-center gap-2">
                    <MaterialIcon name="schedule" className="text-[18px]" /> Giờ hoạt động
                  </div>
                  <div className="md:col-span-2 font-d-body-md text-d-body-md font-medium text-d-on-surface">07:00 - 18:00 (Thứ 2 - Thứ 7)</div>
                </div>
                
                {/* Materials Tags */}
                <div className="pt-2">
                  <div className="text-d-on-surface-variant font-d-label-md text-d-label-md mb-3 flex items-center gap-2">
                    <MaterialIcon name="recycling" className="text-[18px]" /> Vật liệu thu mua
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <span className="px-4 py-1.5 rounded-full bg-d-surface-container text-d-on-surface font-d-body-sm text-d-body-sm border border-d-outline-variant">Nhựa</span>
                    <span className="px-4 py-1.5 rounded-full bg-d-surface-container text-d-on-surface font-d-body-sm text-d-body-sm border border-d-outline-variant">Giấy</span>
                    <span className="px-4 py-1.5 rounded-full bg-d-surface-container text-d-on-surface font-d-body-sm text-d-body-sm border border-d-outline-variant">Sắt thép</span>
                    <span className="px-4 py-1.5 rounded-full bg-d-surface-container text-d-on-surface font-d-body-sm text-d-body-sm border border-d-outline-variant">Đồng</span>
                    <span className="px-4 py-1.5 rounded-full bg-d-surface-container text-d-on-surface font-d-body-sm text-d-body-sm border border-d-outline-variant">Nhôm</span>
                  </div>
                </div>
              </div>
            </div>

            {/* Vị trí trên bản đồ */}
            <div className="bg-d-surface-container-lowest rounded-[20px] p-6 md:p-8 border border-d-border-subtle shadow-sm">
              <h2 className="font-d-headline-md text-d-headline-md font-semibold text-d-on-surface mb-6 flex items-center gap-2">
                <MaterialIcon name="map" className="text-d-primary" />
                Vị trí trên bản đồ
              </h2>
              <div className="w-full h-[300px] rounded-xl bg-d-surface-container-high overflow-hidden relative border border-d-border-subtle">
                {/* Map Image Placeholder */}
                <div 
                  className="w-full h-full bg-cover bg-center" 
                  style={{ backgroundImage: "url('https://lh3.googleusercontent.com/aida-public/AB6AXuAhxYXZE1lZkfmMEoRd1mRa96wW6k0_Zq9dt4YoUdlWhvY1EQQZ3vAAwvva9jayLqa90k1iLhjeBx3pfhQN-plni0WgwM-6twWIgJCga9nUmaLlIYiGYkwbQF-84pxCuBxoEUCsjrei2-Rgg8LEDkXUvdTxQOr27RUsjwW3UoRr9aG5DCoHD0Vvrjs0KRWHxKR0urK3yTLK5Kar3t_NVxdNeHsmBKzu2cZ3oWpDEmUPHbxWvUmqxzN0FQ')" }}
                ></div>
                {/* Custom Lime Green Pin */}
                <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 flex flex-col items-center drop-shadow-md">
                  <div className="w-8 h-8 rounded-full bg-d-primary flex items-center justify-center text-white relative z-10 border-2 border-white">
                    <MaterialIcon name="warehouse" className="text-[18px]" />
                  </div>
                  <div className="w-1 h-4 bg-d-primary -mt-1 z-0"></div>
                  <div className="w-4 h-1.5 rounded-full bg-black/20 blur-[1px] mt-0.5"></div>
                </div>
              </div>
            </div>
          </div>

          {/* Right Column (Stats & Reviews) */}
          <div className="lg:col-span-5 flex flex-col gap-6 md:gap-8">
            {/* Thống kê hoạt động */}
            <div className="bg-d-surface-container-lowest rounded-[20px] p-6 border border-d-border-subtle shadow-sm">
              <h2 className="font-d-headline-md text-d-headline-md font-semibold text-d-on-surface mb-6 flex items-center gap-2">
                <MaterialIcon name="bar_chart" className="text-d-primary" />
                Thống kê hoạt động
              </h2>
              
              <div className="grid grid-cols-2 gap-4">
                {/* Stat Card 1 */}
                <div className="bg-d-surface-container rounded-xl p-4 flex flex-col gap-2 border border-transparent hover:border-d-outline-variant transition-colors">
                  <div className="w-10 h-10 rounded-full bg-d-surface-container-lowest flex items-center justify-center text-d-primary shadow-sm">
                    <MaterialIcon name="task_alt" />
                  </div>
                  <span className="text-d-on-surface-variant font-d-label-sm text-d-label-sm mt-2">Đơn hàng hoàn thành</span>
                  <span className="font-d-headline-md text-d-headline-md font-bold text-d-on-surface">1,245</span>
                </div>
                
                {/* Stat Card 2 */}
                <div className="bg-d-surface-container rounded-xl p-4 flex flex-col gap-2 border border-transparent hover:border-d-outline-variant transition-colors">
                  <div className="w-10 h-10 rounded-full bg-d-surface-container-lowest flex items-center justify-center text-d-primary shadow-sm">
                    <MaterialIcon name="scale" />
                  </div>
                  <span className="text-d-on-surface-variant font-d-label-sm text-d-label-sm mt-2">Tổng khối lượng (kg)</span>
                  <span className="font-d-headline-md text-d-headline-md font-bold text-d-on-surface">45,000</span>
                </div>
                
                {/* Stat Card 3 */}
                <div className="bg-d-surface-container rounded-xl p-4 flex flex-col gap-2 border border-transparent hover:border-d-outline-variant transition-colors">
                  <div className="w-10 h-10 rounded-full bg-d-surface-container-lowest flex items-center justify-center text-[#F59E0B] shadow-sm">
                    <MaterialIcon name="star" style={{ fontVariationSettings: "'FILL' 1" }} />
                  </div>
                  <span className="text-d-on-surface-variant font-d-label-sm text-d-label-sm mt-2">Điểm đánh giá</span>
                  <span className="font-d-headline-md text-d-headline-md font-bold text-d-on-surface">4.5</span>
                </div>
                
                {/* Stat Card 4 */}
                <div className="bg-d-surface-container rounded-xl p-4 flex flex-col gap-2 border border-transparent hover:border-d-outline-variant transition-colors">
                  <div className="w-10 h-10 rounded-full bg-d-surface-container-lowest flex items-center justify-center text-d-primary shadow-sm">
                    <MaterialIcon name="event" />
                  </div>
                  <span className="text-d-on-surface-variant font-d-label-sm text-d-label-sm mt-2">Tham gia từ</span>
                  <span className="font-d-headline-md text-d-headline-md font-bold text-d-on-surface">01/2024</span>
                </div>
              </div>
            </div>

            {/* Đánh giá từ Người bán */}
            <div className="bg-d-surface-container-lowest rounded-[20px] p-6 border border-d-border-subtle flex-1 flex flex-col shadow-sm">
              <div className="flex items-center justify-between mb-6">
                <h2 className="font-d-headline-md text-d-headline-md font-semibold text-d-on-surface flex items-center gap-2">
                  <MaterialIcon name="forum" className="text-d-primary" />
                  Đánh giá mới nhất
                </h2>
                <button className="text-d-primary font-d-label-md text-d-label-md hover:underline">Xem tất cả</button>
              </div>
              
              <div className="space-y-4 flex-1">
                {/* Review Item 1 */}
                <div className="p-4 rounded-xl bg-d-surface-container-low border border-d-border-subtle">
                  <div className="flex items-center justify-between mb-2">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-full bg-d-surface-container-high overflow-hidden border border-d-border-subtle">
                        <img className="w-full h-full object-cover" src="https://lh3.googleusercontent.com/aida-public/AB6AXuC5a0DXW-T7yt5Jrk6laKbJe9Z_tUtGIPsWqFZqRtmDeBG-poPpEuV5qf-CQEIA-VNqkw50avJP2qzum_JWfmQT3IlgAl173A4mnMttSysMjpI6b5fsVp61x8j5Ie0Eoll8adFbKd8NrOiwTim0unR9nkYuxQQ3Vrypim8Q8KfktPTcvP545B_OEwXRhU_8DMx7Ig0_CvGb9wEBwIq-pbwD-c2ghjaobqReGvMFXWPGdujVHenAXy939w" alt="Avatar" />
                      </div>
                      <span className="font-d-label-md text-d-label-md text-d-on-surface font-semibold">Trần Thị B.</span>
                    </div>
                    <span className="text-d-on-surface-variant font-d-label-sm text-d-label-sm">2 ngày trước</span>
                  </div>
                  <div className="flex items-center gap-0.5 text-[#F59E0B] mb-2 text-sm">
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                  </div>
                  <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant line-clamp-3">
                    Kho thu mua giá tốt, cân đo minh bạch. Nhân viên nhiệt tình hỗ trợ bốc vác đồ nặng. Sẽ tiếp tục hợp tác lâu dài.
                  </p>
                </div>
                
                {/* Review Item 2 */}
                <div className="p-4 rounded-xl bg-d-surface-container-low border border-d-border-subtle">
                  <div className="flex items-center justify-between mb-2">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-full bg-d-surface-container-high overflow-hidden border border-d-border-subtle">
                        <img className="w-full h-full object-cover" src="https://lh3.googleusercontent.com/aida-public/AB6AXuCT-bBkjVfX7_zuMmZm3wsaRXkG7W1LTJLp3j0qgTEGGcDwEDaWxG4Qplp29tTdQfqs432Ikw90_UCJawdeY2yRYkaOOJy_P0hHdBGkUDwYD5rnqu_MxRtcQxNTrjQbj5z15KpNy-tNLbwLkg3u9n5t9SsfpqPYLEPYCBFZMOH2IZ3p7BjPCbKRekb62a7EGpwBwSAtPVBRJ2-LFv2Ix8W83G5YbW6sNshJmEfjffKF-cerusko2MZcEA" alt="Avatar" />
                      </div>
                      <span className="font-d-label-md text-d-label-md text-d-on-surface font-semibold">Lê Văn C.</span>
                    </div>
                    <span className="text-d-on-surface-variant font-d-label-sm text-d-label-sm">1 tuần trước</span>
                  </div>
                  <div className="flex items-center gap-0.5 text-[#F59E0B] mb-2 text-sm">
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px] text-d-surface-variant" style={{ fontVariationSettings: "'FILL' 1" }} />
                  </div>
                  <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant line-clamp-3">
                    Quy trình xử lý nhanh chóng, thanh toán ngay lập tức. Tuy nhiên đôi khi kho hơi đông vào buổi sáng nên phải đợi một chút.
                  </p>
                </div>

                {/* Review Item 3 */}
                <div className="p-4 rounded-xl bg-d-surface-container-low border border-d-border-subtle">
                  <div className="flex items-center justify-between mb-2">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-full bg-d-surface-container-high flex items-center justify-center text-d-on-surface-variant font-d-label-md text-d-label-md border border-d-border-subtle">
                        H
                      </div>
                      <span className="font-d-label-md text-d-label-md text-d-on-surface font-semibold">Hoàng D.</span>
                    </div>
                    <span className="text-d-on-surface-variant font-d-label-sm text-d-label-sm">2 tuần trước</span>
                  </div>
                  <div className="flex items-center gap-0.5 text-[#F59E0B] mb-2 text-sm">
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                    <MaterialIcon name="star_half" className="text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }} />
                  </div>
                  <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant line-clamp-2">
                    Rất hài lòng với dịch vụ. Ứng dụng RETRACK kết nối nhanh chóng.
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Profile;

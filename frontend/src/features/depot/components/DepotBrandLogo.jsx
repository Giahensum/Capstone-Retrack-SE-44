import { BrandMark } from '@/components/brand/BrandLogo';

// Giữ API cũ cho Seller/Depot; dùng chung biểu tượng thương hiệu.
export default function DepotBrandLogo({ small = false }) {
  return <BrandMark size={small ? 32 : 40} />;
}

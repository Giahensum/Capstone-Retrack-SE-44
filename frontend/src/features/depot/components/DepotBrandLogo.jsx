export default function DepotBrandLogo({ small = false }) {
  return <span aria-hidden="true" className={`relative block shrink-0 overflow-hidden ${small ? 'h-8 w-8' : 'h-10 w-10'}`}>
    <img src="/brand/logo_nobg.png" alt="" className={`absolute max-w-none object-contain ${small ? '-left-[40px] -top-[12px] h-[62px] w-[112px]' : '-left-[50px] -top-[15px] h-[77px] w-[140px]'}`} />
  </span>;
}

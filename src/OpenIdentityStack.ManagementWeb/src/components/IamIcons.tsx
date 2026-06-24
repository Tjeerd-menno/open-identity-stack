import type { SVGProps } from 'react';

type IconProps = SVGProps<SVGSVGElement>;

function IconBase({ children, ...props }: IconProps) {
  return (
    <svg
      aria-hidden="true"
      fill="none"
      focusable="false"
      height="1.05em"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth={1.8}
      viewBox="0 0 24 24"
      width="1.05em"
      {...props}
    >
      {children}
    </svg>
  );
}

export function OverviewIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M4 5h7v6H4z" />
      <path d="M13 5h7v4h-7z" />
      <path d="M13 11h7v8h-7z" />
      <path d="M4 13h7v6H4z" />
    </IconBase>
  );
}

export function UsersIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M16 18c0-2.2-1.8-4-4-4H7c-2.2 0-4 1.8-4 4" />
      <circle cx="9.5" cy="7.5" r="3" />
      <path d="M21 18c0-1.8-1.2-3.3-2.9-3.8" />
      <path d="M16 5.2a3 3 0 0 1 0 5.6" />
    </IconBase>
  );
}

export function RolesIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M12 3 5 6v5c0 4.4 2.9 8.2 7 9.5 4.1-1.3 7-5.1 7-9.5V6z" />
      <path d="m9 12 2 2 4-5" />
    </IconBase>
  );
}

export function GroupsIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M7 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6z" />
      <path d="M17 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6z" />
      <path d="M2.5 19c.4-2.8 2.3-5 4.5-5s4.1 2.2 4.5 5" />
      <path d="M12.5 19c.4-2.8 2.3-5 4.5-5s4.1 2.2 4.5 5" />
    </IconBase>
  );
}

export function ApplicationsIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M4 5h16v11H4z" />
      <path d="M8 20h8" />
      <path d="M12 16v4" />
      <path d="M8 9h3" />
      <path d="M8 12h8" />
    </IconBase>
  );
}

export function PermissionsIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <circle cx="8" cy="12" r="3" />
      <path d="M11 12h10" />
      <path d="M17 12v3" />
      <path d="M14 12v2" />
    </IconBase>
  );
}

export function SessionsIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M4 6h16v12H4z" />
      <path d="M8 10h5" />
      <path d="M8 14h8" />
      <path d="M17 10h.01" />
    </IconBase>
  );
}

export function ProvidersIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M12 3v18" />
      <path d="M4 8h16" />
      <path d="M4 16h16" />
      <path d="M6 8c1.4-3.2 3.4-5 6-5s4.6 1.8 6 5" />
      <path d="M6 16c1.4 3.2 3.4 5 6 5s4.6-1.8 6-5" />
    </IconBase>
  );
}

export function SettingsIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z" />
      <path d="M12 2v3" />
      <path d="M12 19v3" />
      <path d="m4.9 4.9 2.1 2.1" />
      <path d="m17 17 2.1 2.1" />
      <path d="M2 12h3" />
      <path d="M19 12h3" />
      <path d="m4.9 19.1 2.1-2.1" />
      <path d="m17 7 2.1-2.1" />
    </IconBase>
  );
}

export function AuditIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M7 4h10l3 3v13H7z" />
      <path d="M17 4v4h4" />
      <path d="M4 8v12" />
      <path d="M10 12h7" />
      <path d="M10 16h5" />
    </IconBase>
  );
}

export function SearchIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <circle cx="10.5" cy="10.5" r="5.5" />
      <path d="m15 15 5 5" />
    </IconBase>
  );
}

export function SunIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <circle cx="12" cy="12" r="3.5" />
      <path d="M12 2.5v2" />
      <path d="M12 19.5v2" />
      <path d="m4.6 4.6 1.4 1.4" />
      <path d="m18 18 1.4 1.4" />
      <path d="M2.5 12h2" />
      <path d="M19.5 12h2" />
      <path d="m4.6 19.4 1.4-1.4" />
      <path d="m18 6 1.4-1.4" />
    </IconBase>
  );
}

export function MoonIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M20 15.5A8 8 0 0 1 8.5 4a6.5 6.5 0 1 0 11.5 11.5z" />
    </IconBase>
  );
}

export function SystemThemeIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M4 5h16v11H4z" />
      <path d="M8 20h8" />
      <path d="M12 16v4" />
      <path d="M8 9h8" />
      <path d="M8 12h5" />
    </IconBase>
  );
}

export function HorizontalScrollIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M8 7 3 12l5 5" />
      <path d="M16 7l5 5-5 5" />
      <path d="M4 12h16" />
    </IconBase>
  );
}

export function CloseIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M6 6l12 12" />
      <path d="M18 6 6 18" />
    </IconBase>
  );
}

export function ChevronDownIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="m6 9 6 6 6-6" />
    </IconBase>
  );
}

export function DotsIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <circle cx="5" cy="12" r="1" fill="currentColor" stroke="none" />
      <circle cx="12" cy="12" r="1" fill="currentColor" stroke="none" />
      <circle cx="19" cy="12" r="1" fill="currentColor" stroke="none" />
    </IconBase>
  );
}

export function ViewIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6z" />
      <circle cx="12" cy="12" r="2.5" />
    </IconBase>
  );
}

export function EditIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M4 20h4l10.5-10.5a2.1 2.1 0 0 0-3-3L5 17z" />
      <path d="m14 7 3 3" />
    </IconBase>
  );
}

export function DeleteIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M4 7h16" />
      <path d="M10 11v6" />
      <path d="M14 11v6" />
      <path d="M6 7l1 14h10l1-14" />
      <path d="M9 7V4h6v3" />
    </IconBase>
  );
}

export function ExpandIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M8 3H3v5" />
      <path d="M16 3h5v5" />
      <path d="M8 21H3v-5" />
      <path d="M16 21h5v-5" />
      <path d="M3 3l7 7" />
      <path d="m21 3-7 7" />
      <path d="m3 21 7-7" />
      <path d="m21 21-7-7" />
    </IconBase>
  );
}

export function LogoutIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M10 6H5v12h5" />
      <path d="M14 8l4 4-4 4" />
      <path d="M18 12H9" />
    </IconBase>
  );
}

export function ArrowLeftIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M20 12H4" />
      <path d="m10 6-6 6 6 6" />
    </IconBase>
  );
}

export function ChevronRightIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="m9 6 6 6-6 6" />
    </IconBase>
  );
}

export function ArrowRightIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M4 12h16" />
      <path d="m14 6 6 6-6 6" />
    </IconBase>
  );
}

export function BellIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M18 8a6 6 0 1 0-12 0c0 6-3 7-3 7h18s-3-1-3-7" />
      <path d="M13.7 19a2 2 0 0 1-3.4 0" />
    </IconBase>
  );
}

export function ServerIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <rect x="3" y="4" width="18" height="7" rx="1.5" />
      <rect x="3" y="13" width="18" height="7" rx="1.5" />
      <path d="M7 7.5h.01" />
      <path d="M7 16.5h.01" />
    </IconBase>
  );
}

export function ShieldIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M12 3 5 6v5c0 4.4 2.9 8.2 7 9.5 4.1-1.3 7-5.1 7-9.5V6z" />
    </IconBase>
  );
}

export function AppWindowIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <rect x="3" y="5" width="18" height="14" rx="2" />
      <path d="M3 9h18" />
      <path d="M7 7h.01" />
      <path d="M10 7h.01" />
    </IconBase>
  );
}

export function PlusIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M12 5v14" />
      <path d="M5 12h14" />
    </IconBase>
  );
}

export function RefreshIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <path d="M21 12a9 9 0 1 1-2.6-6.4" />
      <path d="M21 4v5h-5" />
    </IconBase>
  );
}

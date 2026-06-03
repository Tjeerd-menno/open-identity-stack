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

export function DotsIcon(props: IconProps) {
  return (
    <IconBase {...props}>
      <circle cx="5" cy="12" r="1" fill="currentColor" stroke="none" />
      <circle cx="12" cy="12" r="1" fill="currentColor" stroke="none" />
      <circle cx="19" cy="12" r="1" fill="currentColor" stroke="none" />
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


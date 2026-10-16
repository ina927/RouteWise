export function EndpointPin({
  role,
  className,
}: {
  role: 'start' | 'end'
  className?: string
}) {
  return (
    <svg
      viewBox="0 0 32 40"
      width="32"
      height="40"
      className={className}
      aria-hidden="true"
      focusable="false"
      style={{ color: `var(--color-route-${role})`, flexShrink: 0 }}
    >
      <path
        d="M16 1a14 14 0 0 0-14 14c0 10 14 24 14 24s14-14 14-24A14 14 0 0 0 16 1Z"
        fill="currentColor"
      />
      <text
        x="16"
        y="21"
        textAnchor="middle"
        fill="var(--color-text-on-solid)"
        fontSize="17"
        fontWeight="700"
      >
        {role === 'start' ? 'S' : 'E'}
      </text>
    </svg>
  )
}

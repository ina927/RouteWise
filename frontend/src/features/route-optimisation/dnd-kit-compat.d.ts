import type { JSX as ReactJSX } from 'react'

// Installed dnd-kit declarations still refer to the pre-React-19 JSX namespace.
// Keep declaration checking enabled; supply only the aliases they require.
declare global {
  namespace JSX {
    type Element = ReactJSX.Element
    type IntrinsicElements = ReactJSX.IntrinsicElements
  }
}

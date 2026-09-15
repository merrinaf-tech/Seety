/** Coordinates and dimensions are all in rem. Oversized panels stay anchored at the top left. */
export function clampPosition(
  position: { x: number; y: number },
  viewport: { width: number; height: number },
  size: { width: number; height: number }
): { x: number; y: number } {
  return {
    x: Math.min(Math.max(0, position.x), Math.max(0, viewport.width - size.width)),
    y: Math.min(Math.max(0, position.y), Math.max(0, viewport.height - size.height)),
  };
}

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

/**
 * A window's size while its corner is dragged, in rem: no smaller than the minimum, and no larger
 * than the room left between its top-left corner and the screen's edges. A minimum larger than
 * that room wins, so a window near the edge never collapses below a usable size.
 */
export function clampSize(
  size: { width: number; height: number },
  minimum: { width: number; height: number },
  room: { width: number; height: number }
): { width: number; height: number } {
  return {
    width: Math.max(minimum.width, Math.min(size.width, room.width)),
    height: Math.max(minimum.height, Math.min(size.height, room.height)),
  };
}

/**
 * Kartlag der forklaringen skal hentes fra andre WMS-lag enn de som tegnes.
 * Administrative grenser tegnes fra flere målestokknivåer som hver gjentar de samme tegnene,
 * så forklaringen bruker bare ett nivå (N5000).
 */
export const MAP_LAYER_LEGEND_LAYERS: Record<string, string[]> = {
  admGrenser: ['N5000AdministrativeGrenser'],
};

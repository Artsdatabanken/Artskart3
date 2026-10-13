export interface MapLayerDto {
  id: number;
  name: string;
  type: string;
  url: string;
  layers?: string | null;
  format?: string | null;
  version?: string | null;
  attribution?: string | null;
  minScaleDenominator?: number | null;
  maxScaleDenominator?: number | null;
  useSingleImage?: boolean;
  retryAfterSeconds?: number;
  opacity?: number;
}

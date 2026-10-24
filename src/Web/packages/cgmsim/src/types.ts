export interface ConnectorSummary {
  name?: string;
  connectorName?: string;
  isHealthy?: boolean;
  lastSuccessfulSync?: string | null;
  lastErrorMessage?: string | null;
  lastError?: string | null;
}

export interface TenantSummary {
  id: string;
  slug: string;
  displayName: string;
  isActive: boolean;
  entries?: number;
  latestEntry?: string | null;
  connectors?: ConnectorSummary[];
}

export interface CgmSimSubject {
  sex: string;
  weight: number;
  age: number;
  height: number;
}

export interface CgmSimParameters {
  isf: number;
  dia: number;
  carbsAbsTime: number;
  insulinPeak: number;
  cr: number;
}

export interface CgmSimSettings {
  subject: CgmSimSubject;
  parameters: CgmSimParameters;
  isInitialized: boolean;
}

export interface Treatment {
  _id?: string;
  eventType?: string;
  insulin?: number | null;
  carbs?: number | null;
  created_at?: string;
  date?: number;
  mills?: number;
}

export type TrendDirection =
  | "DoubleUp"
  | "SingleUp"
  | "FortyFiveUp"
  | "Flat"
  | "FortyFiveDown"
  | "SingleDown"
  | "DoubleDown";

export interface SimulatedEntry {
  type: "sgv";
  sgv: number;
  direction: TrendDirection;
  date: number;
  dateString: string;
  device: string;
}

export interface CurrentEntry {
  _id?: string;
  sgv?: number;
  date?: number;
  mills?: number;
  dateString?: string;
  direction?: TrendDirection;
  device?: string;
}

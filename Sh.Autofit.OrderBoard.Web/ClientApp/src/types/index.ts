export type OrderStage = 'ORDER_IN_PC' | 'ORDER_PRINTED' | 'DOC_IN_PC' | 'PACKING' | 'PACKED';

export const STAGES: OrderStage[] = ['ORDER_IN_PC', 'ORDER_PRINTED', 'DOC_IN_PC', 'PACKING', 'PACKED'];

export const STAGE_LABELS: Record<OrderStage, string> = {
  ORDER_IN_PC: 'הזמנה במחשב',
  ORDER_PRINTED: 'הזמנה הודפסה',
  DOC_IN_PC: 'מסמך במחשב',
  PACKING: 'אריזה',
  PACKED: 'נארז',
};

export const STAGE_COLORS: Record<OrderStage, string> = {
  ORDER_IN_PC: 'bg-blue-600',
  ORDER_PRINTED: 'bg-amber-600',
  DOC_IN_PC: 'bg-purple-600',
  PACKING: 'bg-green-600',
  PACKED: 'bg-teal-600',
};

export interface OrderCard {
  appOrderId: number;
  accountKey: string;
  accountName: string | null;
  city: string | null;
  address: string | null;
  phone: string | null;
  displayTime: string | null;
  currentStage: OrderStage;
  pinned: boolean;
  hidden: boolean;
  isManual: boolean;
  manualNote: string | null;
  deliveryMethodId: number | null;
  deliveryMethodName: string | null;
  deliveryRunId: number | null;
  needsResolve: boolean;
  stackCount: number;
  stackedOrderIds: number[] | null;
  createdAt: string;
  stageUpdatedAt: string;
  closestRuleName: string | null;
  closestRuleWindow: string | null;
  hasPackedSibling: boolean;
  // Archive-only fields (null on live board)
  packedAt: string | null;
  packedDuration: string | null;
  hiddenAt: string | null;
}

export interface DeliveryGroup {
  title: string;
  deliveryMethodId: number | null;
  deliveryRunId: number | null;
  timeWindow: string | null;
  count: number;
  orders: OrderCard[];
}

export interface BoardColumn {
  stage: OrderStage;
  label: string;
  count: number;
  groups: DeliveryGroup[];
}

export interface DeliveryMethodDto {
  deliveryMethodId: number;
  name: string;
  isAdHoc: boolean;
  isActive: boolean;
  windowStartTime: string | null;
  windowEndTime: string | null;
  runs: DeliveryRunDto[];
}

export interface DeliveryRunDto {
  deliveryRunId: number;
  state: string;
  windowStart: string;
  windowEnd: string;
}

export interface BoardResponse {
  columns: BoardColumn[];
  deliveryMethods: DeliveryMethodDto[];
  timestamp: string;
}

export interface DeliveryMethodFull {
  deliveryMethodId: number;
  name: string;
  isActive: boolean;
  isAdHoc: boolean;
  createdAt: string;
  closedAt: string | null;
  windowStartTime: string | null;
  windowEndTime: string | null;
  autoHideAfterMinutes: number | null;
}

export interface CustomerRule {
  id: number;
  accountKey: string;
  deliveryMethodId: number;
  windowStart: string | null;
  windowEnd: string | null;
  daysOfWeek: string | null;
  isActive: boolean;
}

export interface AccountSearchResult {
  accountKey: string;
  fullName: string | null;
  city: string | null;
  phone: string | null;
}

// ---- Archive ----

export interface ArchiveDaySummary {
  totalOrders: number;
  ordersPacked: number;
  ordersCreated: number;
  byStage: Record<string, number>;
}

export interface ArchiveBoardResponse {
  date: string;
  columns: BoardColumn[];
  deliveryMethods: DeliveryMethodDto[];
  summary: ArchiveDaySummary;
}

export interface OrderTimelineEvent {
  eventId: number;
  at: string;
  action: string;
  fromStage: string | null;
  toStage: string | null;
  actor: string | null;
}

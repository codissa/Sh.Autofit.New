import type { OrderCard as OrderCardType } from '../../types';

type SlaStatus = 'none' | 'green' | 'yellow' | 'red';

function getSlaStatus(stage: string, stageUpdatedAt: string): SlaStatus {
  if (stage !== 'ORDER_PRINTED' && stage !== 'PACKING' && stage !== 'PACKED') return 'none';
  const mins = (Date.now() - new Date(stageUpdatedAt).getTime()) / 60000;
  if (mins < 30) return 'green';
  if (mins < 60) return 'yellow';
  return 'red';
}

const SLA_BORDER: Record<SlaStatus, string> = {
  none: '',
  green: 'border-r-4 border-r-green-500',
  yellow: 'border-r-4 border-r-yellow-500',
  red: 'border-r-4 border-r-red-500',
};

interface Props {
  order: OrderCardType;
  onShowTimeline?: (appOrderId: number) => void;
}

export default function ArchiveOrderCard({ order, onShowTimeline }: Props) {
  const createdAtStr = order.createdAt
    ? new Date(order.createdAt).toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })
    : '';
  const stageTimeStr = order.stageUpdatedAt
    ? new Date(order.stageUpdatedAt).toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })
    : '';
  const packedAtStr = order.packedAt
    ? new Date(order.packedAt).toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })
    : null;
  const hiddenAtStr = order.hidden && order.hiddenAt
    ? new Date(order.hiddenAt).toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })
    : null;

  const slaStatus = getSlaStatus(order.currentStage, order.stageUpdatedAt);
  const slaBorder = SLA_BORDER[slaStatus];

  return (
    <div
      onClick={() => onShowTimeline?.(order.appOrderId)}
      className={`relative p-1.5 border rounded-lg shadow-sm transition-shadow select-none
        ${onShowTimeline ? 'cursor-pointer hover:shadow-md hover:bg-gray-50' : ''}
        ${slaBorder}
        ${order.isManual ? 'bg-yellow-50' : 'bg-white'}
        ${order.pinned ? 'border-amber-400 border-2' : 'border-gray-200'}
        ${order.hidden ? 'opacity-60' : ''}
        ${order.needsResolve ? 'ring-2 ring-red-400' : ''}`}
    >
      {/* Stack badge */}
      {order.stackCount > 1 && (
        <span className="absolute -top-2 -left-2 bg-red-500 text-white text-[10px] font-bold rounded-full w-5 h-5 flex items-center justify-center">
          {order.stackCount}
        </span>
      )}

      {/* Row 1: AccountKey */}
      <div className="flex items-center justify-between">
        <span className="font-black text-lg text-gray-900 leading-tight">{order.accountKey}</span>
        {order.pinned && <span className="text-xs text-amber-500">📌</span>}
      </div>

      {/* Row 2: AccountName */}
      <div className="text-lg font-semibold text-gray-700 truncate leading-tight">{order.accountName}</div>

      {/* Row 3: City/Address */}
      {(order.city || order.address) && (
        <div className="text-[10px] text-gray-400 truncate">
          {[order.city, order.address].filter(Boolean).join(' · ')}
        </div>
      )}

      {/* Row 4: Times + Badges */}
      <div className="flex items-center justify-between mt-0.5">
        <div className="flex items-center gap-1.5 text-[10px] text-gray-400">
          {createdAtStr && <span title="זמן קליטה">{createdAtStr}</span>}
          {stageTimeStr && createdAtStr !== stageTimeStr && (
            <span title="כניסה לשלב" className="text-gray-500 font-medium">{stageTimeStr}</span>
          )}
        </div>
        <div className="flex gap-0.5 flex-wrap justify-end">
          {order.isManual && (
            <span className="text-[10px] bg-yellow-200 text-yellow-800 px-1 py-0.5 rounded">ידני</span>
          )}
          {order.deliveryMethodName && (
            <span className="text-[10px] bg-blue-100 text-blue-700 px-1 py-0.5 rounded truncate max-w-[70px]">
              {order.deliveryMethodName}
            </span>
          )}
        </div>
      </div>

      {/* Packed timestamp — prominent display */}
      {packedAtStr && (
        <div className="mt-1 flex items-center justify-between bg-teal-50 rounded px-1.5 py-0.5">
          <span className="text-[11px] font-semibold text-teal-700">
            נארז ב: {packedAtStr}
          </span>
          {order.packedDuration && (
            <span className="text-[10px] text-teal-600 bg-teal-100 px-1 rounded">
              {order.packedDuration}
            </span>
          )}
        </div>
      )}

      {/* Hidden timestamp */}
      {hiddenAtStr && (
        <div className="mt-0.5 flex items-center bg-gray-100 rounded px-1.5 py-0.5">
          <span className="text-[10px] text-gray-500">
            הוסתר ב: {hiddenAtStr}
          </span>
        </div>
      )}

      {/* Manual note */}
      {order.manualNote && (
        <div className="text-[10px] text-orange-600 truncate" title={order.manualNote}>
          📝 {order.manualNote}
        </div>
      )}
    </div>
  );
}

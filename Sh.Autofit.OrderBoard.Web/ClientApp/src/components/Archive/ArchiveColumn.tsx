import type { BoardColumn, OrderCard as OrderCardType } from '../../types';
import { STAGE_COLORS, type OrderStage } from '../../types';
import ArchiveOrderCard from './ArchiveOrderCard';

interface Props {
  column: BoardColumn;
  onShowTimeline?: (appOrderId: number) => void;
}

export default function ArchiveColumn({ column, onShowTimeline }: Props) {
  const color = STAGE_COLORS[column.stage as OrderStage] ?? 'bg-gray-600';
  const hasGroups = column.groups.length > 1 ||
    (column.groups.length === 1 && column.groups[0].title !== '');

  return (
    <div className="flex flex-col min-w-[220px] w-[220px] max-h-[calc(100vh-160px)] bg-gray-50 rounded-xl shadow overflow-hidden">
      {/* Header */}
      <div className={`${color} text-white px-3 py-2 flex items-center justify-between`}>
        <span className="font-bold text-sm">{column.label}</span>
        <span className="bg-white/20 text-xs font-bold px-2 py-0.5 rounded-full">
          {column.count}
        </span>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-2 space-y-1.5">
        {hasGroups ? (
          column.groups.map((group, i) => (
            <div key={group.title || i} className="border border-dashed border-gray-300 rounded-lg p-1.5">
              {group.title && (
                <div className="flex items-center justify-between mb-1 px-1">
                  <span className="text-xs font-semibold text-gray-600">{group.title}</span>
                  <span className="text-[10px] text-gray-400">{group.count}</span>
                </div>
              )}
              <div className="space-y-1.5">
                {group.orders.map((order: OrderCardType) => (
                  <ArchiveOrderCard
                    key={order.appOrderId}
                    order={order}
                    onShowTimeline={onShowTimeline}
                  />
                ))}
              </div>
              {group.orders.length === 0 && (
                <div className="text-[10px] text-gray-300 text-center py-2">ריק</div>
              )}
            </div>
          ))
        ) : (
          column.groups.flatMap((g) => g.orders).map((order: OrderCardType) => (
            <ArchiveOrderCard
              key={order.appOrderId}
              order={order}
              onShowTimeline={onShowTimeline}
            />
          ))
        )}

        {column.count === 0 && (
          <div className="text-xs text-gray-300 text-center py-8">אין הזמנות</div>
        )}
      </div>
    </div>
  );
}

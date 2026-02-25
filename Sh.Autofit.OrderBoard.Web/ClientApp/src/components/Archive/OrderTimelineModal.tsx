import { useEffect, useState } from 'react';
import type { OrderTimelineEvent } from '../../types';
import { STAGE_LABELS, type OrderStage } from '../../types';
import { getOrderTimeline } from '../../api/client';

const ACTION_LABELS: Record<string, string> = {
  MOVE_STAGE: 'שינוי שלב',
  CREATE_MANUAL: 'יצירה ידנית',
  MERGE_AUTO: 'מיזוג אוטומטי',
  MERGE_STAGE_CORRELATION: 'מיזוג קורלציה',
  HIDE: 'הסתרה',
  UNHIDE: 'ביטול הסתרה',
  BULK_HIDE: 'הסתרה קבוצתית',
  PIN: 'נעיצה',
  UNPIN: 'ביטול נעיצה',
  ASSIGN_DELIVERY: 'שיוך משלוח',
};

function stageLabel(stage: string | null): string {
  if (!stage) return '';
  return STAGE_LABELS[stage as OrderStage] ?? stage;
}

interface Props {
  appOrderId: number;
  onClose: () => void;
}

export default function OrderTimelineModal({ appOrderId, onClose }: Props) {
  const [events, setEvents] = useState<OrderTimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    getOrderTimeline(appOrderId)
      .then((data) => {
        if (!cancelled) setEvents(data);
      })
      .catch((err) => {
        if (!cancelled) setError(String(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [appOrderId]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={onClose}>
      <div
        className="bg-white rounded-xl shadow-2xl w-[420px] max-h-[80vh] flex flex-col"
        dir="rtl"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3 border-b">
          <h2 className="font-bold text-base">היסטוריית הזמנה #{appOrderId}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-lg">✕</button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-4">
          {loading && <div className="text-center text-gray-400 py-8">טוען...</div>}
          {error && <div className="text-center text-red-500 py-4">{error}</div>}
          {!loading && !error && events.length === 0 && (
            <div className="text-center text-gray-400 py-8">אין אירועים</div>
          )}

          {!loading && events.length > 0 && (
            <div className="relative pr-4">
              {/* Timeline line */}
              <div className="absolute right-1.5 top-1 bottom-1 w-0.5 bg-gray-200" />

              <div className="space-y-4">
                {events.map((evt) => (
                  <div key={evt.eventId} className="relative flex gap-3">
                    {/* Dot */}
                    <div className="absolute right-0 top-1.5 w-3 h-3 rounded-full bg-blue-500 border-2 border-white shadow z-10" />

                    {/* Event content */}
                    <div className="mr-5 flex-1">
                      <div className="flex items-center gap-2 text-xs text-gray-400">
                        <span>
                          {new Date(evt.at).toLocaleDateString('he-IL', { day: '2-digit', month: '2-digit' })}
                          {' '}
                          {new Date(evt.at).toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                        </span>
                        {evt.actor && (
                          <span className="text-gray-300">{evt.actor}</span>
                        )}
                      </div>
                      <div className="text-sm font-medium text-gray-700 mt-0.5">
                        {ACTION_LABELS[evt.action] ?? evt.action}
                      </div>
                      {(evt.fromStage || evt.toStage) && (
                        <div className="text-xs text-gray-500 mt-0.5">
                          {evt.fromStage && <span>{stageLabel(evt.fromStage)}</span>}
                          {evt.fromStage && evt.toStage && <span className="mx-1">←</span>}
                          {evt.toStage && (
                            <span className="font-semibold text-gray-700">{stageLabel(evt.toStage)}</span>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

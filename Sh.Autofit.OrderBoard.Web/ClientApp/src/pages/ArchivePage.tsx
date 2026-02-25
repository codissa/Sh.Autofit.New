import { useState, useCallback } from 'react';
import type { ArchiveBoardResponse } from '../types';
import { STAGE_LABELS, type OrderStage } from '../types';
import { getArchiveBoard } from '../api/client';
import ArchiveBoard from '../components/Archive/ArchiveBoard';
import OrderTimelineModal from '../components/Archive/OrderTimelineModal';

function toLocalDateStr(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function yesterday(): string {
  const d = new Date();
  d.setDate(d.getDate() - 1);
  return toLocalDateStr(d);
}

function dayBefore(): string {
  const d = new Date();
  d.setDate(d.getDate() - 2);
  return toLocalDateStr(d);
}

function formatDateHebrew(dateStr: string): string {
  const d = new Date(dateStr + 'T00:00:00');
  const days = ['ראשון', 'שני', 'שלישי', 'רביעי', 'חמישי', 'שישי', 'שבת'];
  return `יום ${days[d.getDay()]} ${d.toLocaleDateString('he-IL')}`;
}

export default function ArchivePage() {
  const [selectedDate, setSelectedDate] = useState('');
  const [board, setBoard] = useState<ArchiveBoardResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [timelineOrderId, setTimelineOrderId] = useState<number | null>(null);

  const loadBoard = useCallback(async (date: string) => {
    if (!date) return;
    setSelectedDate(date);
    setLoading(true);
    setError(null);
    try {
      const data = await getArchiveBoard(date);
      setBoard(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setBoard(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const handleDateChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    loadBoard(e.target.value);
  };

  return (
    <div className="flex flex-col h-[calc(100vh-48px)]" dir="rtl">
      {/* Toolbar */}
      <div className="bg-white border-b shadow-sm px-4 py-2 flex items-center gap-3 flex-wrap">
        <label className="text-sm font-semibold text-gray-700">תאריך:</label>
        <input
          type="date"
          value={selectedDate}
          onChange={handleDateChange}
          max={yesterday()}
          className="border rounded px-2 py-1 text-sm"
        />
        <button
          onClick={() => loadBoard(yesterday())}
          className={`text-xs px-3 py-1 rounded border transition-colors
            ${selectedDate === yesterday()
              ? 'bg-blue-600 text-white border-blue-600'
              : 'bg-white text-gray-600 border-gray-300 hover:bg-gray-50'}`}
        >
          אתמול
        </button>
        <button
          onClick={() => loadBoard(dayBefore())}
          className={`text-xs px-3 py-1 rounded border transition-colors
            ${selectedDate === dayBefore()
              ? 'bg-blue-600 text-white border-blue-600'
              : 'bg-white text-gray-600 border-gray-300 hover:bg-gray-50'}`}
        >
          שלשום
        </button>

        <div className="w-px h-6 bg-gray-200 mx-1" />

        {/* Search */}
        <div className="relative">
          <input
            type="text"
            placeholder="חיפוש..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="border rounded px-2 py-1 text-sm w-40"
          />
          {searchQuery && (
            <button
              onClick={() => setSearchQuery('')}
              className="absolute left-1.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-sm"
            >
              ✕
            </button>
          )}
        </div>

        {/* Date display */}
        {selectedDate && (
          <span className="text-sm text-gray-500 mr-auto">
            {formatDateHebrew(selectedDate)}
          </span>
        )}
      </div>

      {/* Summary stats */}
      {board && !loading && (
        <div className="bg-gray-50 border-b px-4 py-1.5 flex items-center gap-4 text-xs" dir="rtl">
          <span className="font-semibold text-gray-600">
            סה״כ: <span className="text-gray-900">{board.summary.totalOrders}</span>
          </span>
          <span className="text-teal-700">
            נארזו: <span className="font-semibold">{board.summary.ordersPacked}</span>
          </span>
          <span className="text-blue-700">
            נוצרו: <span className="font-semibold">{board.summary.ordersCreated}</span>
          </span>
          <div className="w-px h-4 bg-gray-200" />
          {Object.entries(board.summary.byStage).map(([stage, count]) => (
            <span key={stage} className="text-gray-500">
              {STAGE_LABELS[stage as OrderStage] ?? stage}: {count}
            </span>
          ))}
        </div>
      )}

      {/* Content area */}
      <div className="flex-1 overflow-hidden">
        {!selectedDate && (
          <div className="flex items-center justify-center h-full text-gray-400 text-lg">
            בחר תאריך לצפייה בארכיון
          </div>
        )}

        {loading && (
          <div className="flex items-center justify-center h-full text-gray-400">
            טוען...
          </div>
        )}

        {error && (
          <div className="flex items-center justify-center h-full text-red-500">
            {error}
          </div>
        )}

        {board && !loading && !error && (
          <ArchiveBoard
            board={board}
            searchQuery={searchQuery}
            onShowTimeline={setTimelineOrderId}
          />
        )}
      </div>

      {/* Timeline modal */}
      {timelineOrderId !== null && (
        <OrderTimelineModal
          appOrderId={timelineOrderId}
          onClose={() => setTimelineOrderId(null)}
        />
      )}
    </div>
  );
}

import type { PrintProgress, PrintComplete } from '../types';

interface Props {
  progress: PrintProgress | null;
  result: PrintComplete | null;
  error: string | null;
  onClose: () => void;
}

export default function BatchPrintProgress({ progress, result, error, onClose }: Props) {
  if (!progress && !result && !error) return null;

  const pct = progress ? Math.round((progress.current / progress.total) * 100) : result ? 100 : 0;

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-sm p-6">
        {progress && !result && (
          <>
            <h3 className="text-lg font-semibold mb-4">Printing...</h3>
            <div className="w-full bg-gray-200 rounded-full h-3 mb-3">
              <div
                className="bg-blue-600 h-3 rounded-full transition-all duration-300"
                style={{ width: `${pct}%` }}
              />
            </div>
            <p className="text-sm text-gray-600">
              {progress.current} / {progress.total} — {progress.currentItemKey}
            </p>
          </>
        )}
        {result && (
          <>
            <h3 className="text-lg font-semibold mb-2">
              {result.errors.length === 0 ? 'Print Complete' : 'Print Completed with Errors'}
            </h3>
            <p className="text-sm text-gray-600 mb-2">
              Printed {result.totalPrinted} labels
            </p>
            {result.errors.length > 0 && (
              <ul className="text-sm text-red-600 mb-3 space-y-1">
                {result.errors.map((e, i) => (
                  <li key={i}>{e}</li>
                ))}
              </ul>
            )}
            <button
              onClick={onClose}
              className="w-full py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 text-sm"
            >
              Close
            </button>
          </>
        )}
        {error && !result && (
          <>
            <h3 className="text-lg font-semibold text-red-600 mb-2">Error</h3>
            <p className="text-sm text-gray-600 mb-3">{error}</p>
            <button
              onClick={onClose}
              className="w-full py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 text-sm"
            >
              Close
            </button>
          </>
        )}
      </div>
    </div>
  );
}

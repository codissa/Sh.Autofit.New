import { useState, useCallback } from 'react';
import { getStockInfo, getStockMoves, getPartByKey, printLabel } from '../api/client';
import LanguageSelector from '../components/LanguageSelector';
import PrinterSelector from '../components/PrinterSelector';
import LabelPreview from '../components/LabelPreview';
import ArabicEditor from '../components/ArabicEditor';
import BatchPrintProgress from '../components/BatchPrintProgress';
import { usePrintHub } from '../hooks/usePrintHub';
import type { StockInfo, Language, SortOption, StockMoveDisplayItem, PartInfo } from '../types';

export default function StockMove() {
  const [stockIdInput, setStockIdInput] = useState('');
  const [stockInfo, setStockInfo] = useState<StockInfo | null>(null);
  const [items, setItems] = useState<StockMoveDisplayItem[]>([]);
  const [partInfoMap, setPartInfoMap] = useState<Record<string, PartInfo>>({});
  const [globalLanguage, setGlobalLanguage] = useState<Language>('ar');
  const [sortOption, setSortOption] = useState<SortOption>('localization');
  const [printer, setPrinter] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [printingItem, setPrintingItem] = useState<string | null>(null);
  const [editingArabic, setEditingArabic] = useState<string | null>(null);
  const [previewRefreshKeys, setPreviewRefreshKeys] = useState<Record<string, number>>({});

  const { printBatch, progress, isPrinting, result, error: hubError, reset } = usePrintHub();

  const getDescription = useCallback(
    (itemKey: string, lang: Language, partMap: Record<string, PartInfo>) => {
      const part = partMap[itemKey];
      if (!part) return '';
      return lang === 'ar'
        ? part.arabicDescription || part.hebrewDescription || part.partName
        : part.hebrewDescription || part.partName;
    },
    [],
  );

  async function handleLoadStock() {
    const stockId = parseInt(stockIdInput);
    if (isNaN(stockId)) return;

    setLoading(true);
    setError(null);
    setItems([]);
    setPartInfoMap({});

    try {
      const [info, moves] = await Promise.all([getStockInfo(stockId), getStockMoves(stockId)]);
      setStockInfo(info);

      const partMap: Record<string, PartInfo> = {};
      await Promise.all(
        moves.map(async m => {
          try {
            partMap[m.itemKey] = await getPartByKey(m.itemKey);
          } catch {
            /* item not found in parts DB */
          }
        }),
      );
      setPartInfoMap(partMap);

      const displayItems: StockMoveDisplayItem[] = moves.map(m => ({
        ...m,
        language: globalLanguage,
        description: getDescription(m.itemKey, globalLanguage, partMap),
        quantity: Math.ceil(m.totalQuantity),
      }));

      setItems(sortItems(displayItems, sortOption));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Load failed');
    } finally {
      setLoading(false);
    }
  }

  function sortItems(list: StockMoveDisplayItem[], sort: SortOption): StockMoveDisplayItem[] {
    const sorted = [...list];
    switch (sort) {
      case 'localization':
        sorted.sort((a, b) => (a.localization ?? '').localeCompare(b.localization ?? '') || a.itemKey.localeCompare(b.itemKey));
        break;
      case 'itemKey':
        sorted.sort((a, b) => a.itemKey.localeCompare(b.itemKey));
        break;
      case 'originalOrder':
        sorted.sort((a, b) => a.originalOrder - b.originalOrder);
        break;
    }
    return sorted;
  }

  function handleSort(option: SortOption) {
    setSortOption(option);
    setItems(prev => sortItems(prev, option));
  }

  function handleGlobalLanguage(lang: Language) {
    setGlobalLanguage(lang);
    setItems(prev =>
      prev.map(item => ({
        ...item,
        language: lang,
        description: getDescription(item.itemKey, lang, partInfoMap),
      })),
    );
  }

  function handleItemLanguage(itemKey: string, lang: Language) {
    setItems(prev =>
      prev.map(item =>
        item.itemKey === itemKey
          ? { ...item, language: lang, description: getDescription(itemKey, lang, partInfoMap) }
          : item,
      ),
    );
  }

  function handleRemoveItem(itemKey: string) {
    setItems(prev => prev.filter(item => item.itemKey !== itemKey));
  }

  function handleItemQuantity(itemKey: string, qty: number) {
    setItems(prev =>
      prev.map(item => (item.itemKey === itemKey ? { ...item, quantity: qty } : item)),
    );
  }

  function handleItemQuantityBlur(itemKey: string) {
    setItems(prev =>
      prev.map(item => (item.itemKey === itemKey ? { ...item, quantity: Math.max(1, item.quantity) } : item)),
    );
  }

  async function handlePrintItem(item: StockMoveDisplayItem) {
    if (!printer) return;
    setPrintingItem(item.itemKey);
    try {
      await printLabel({
        itemKey: item.itemKey,
        language: item.language,
        quantity: item.quantity,
        printerName: printer,
      });
    } catch {
      /* error handled by UI */
    } finally {
      setPrintingItem(null);
    }
  }

  function handlePrintAll() {
    if (!printer || items.length === 0) return;
    printBatch(
      items.map(i => ({ itemKey: i.itemKey, language: i.language, quantity: i.quantity })),
      printer,
      globalLanguage,
    );
  }

  function handleArabicSaved(itemKey: string, newDesc: string) {
    setPartInfoMap(prev => ({
      ...prev,
      [itemKey]: { ...prev[itemKey], arabicDescription: newDesc },
    }));
    setItems(prev =>
      prev.map(item =>
        item.itemKey === itemKey && item.language === 'ar'
          ? { ...item, description: newDesc }
          : item,
      ),
    );
    setPreviewRefreshKeys(prev => ({ ...prev, [itemKey]: (prev[itemKey] ?? 0) + 1 }));
    setEditingArabic(null);
  }

  function handleArabicDeleted(itemKey: string) {
    setPartInfoMap(prev => ({
      ...prev,
      [itemKey]: { ...prev[itemKey], arabicDescription: null },
    }));
    setItems(prev =>
      prev.map(item => {
        if (item.itemKey !== itemKey) return item;
        const part = partInfoMap[itemKey];
        return {
          ...item,
          description: item.language === 'ar'
            ? part?.hebrewDescription || part?.partName || ''
            : item.description,
        };
      }),
    );
    setPreviewRefreshKeys(prev => ({ ...prev, [itemKey]: (prev[itemKey] ?? 0) + 1 }));
    setEditingArabic(null);
  }

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-bold text-gray-800">Stock Move</h2>

      {/* Load Stock */}
      <div className="flex gap-2">
        <input
          type="number"
          value={stockIdInput}
          onChange={e => setStockIdInput(e.target.value)}
          placeholder="Stock ID"
          className="flex-1 border border-gray-300 rounded-lg px-3 py-2.5 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          onKeyDown={e => e.key === 'Enter' && handleLoadStock()}
        />
        <button
          onClick={handleLoadStock}
          disabled={loading || !stockIdInput}
          className="px-6 py-2.5 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-300 text-sm font-medium"
        >
          {loading ? 'Loading...' : 'Load'}
        </button>
      </div>

      {error && <p className="text-red-600 text-sm">{error}</p>}

      {/* Stock Header */}
      {stockInfo && (
        <div className="bg-gray-100 rounded-lg p-3 text-sm space-y-1">
          <p><strong>Stock #{stockInfo.stockId}</strong> — {stockInfo.accountName} ({stockInfo.accountKey})</p>
          {stockInfo.valueDate && <p>Date: {new Date(stockInfo.valueDate).toLocaleDateString()}</p>}
          {stockInfo.remarks && <p className="text-gray-500">{stockInfo.remarks}</p>}
        </div>
      )}

      {/* Controls */}
      {items.length > 0 && (
        <div className="flex flex-wrap items-center gap-3 bg-white border border-gray-200 rounded-lg p-3">
          <LanguageSelector value={globalLanguage} onChange={handleGlobalLanguage} />

          <select
            value={sortOption}
            onChange={e => handleSort(e.target.value as SortOption)}
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm"
          >
            <option value="localization">Sort: Location</option>
            <option value="itemKey">Sort: Item Key</option>
            <option value="originalOrder">Sort: Original</option>
          </select>

          <div className="flex-1 min-w-[200px]">
            <PrinterSelector value={printer} onChange={setPrinter} />
          </div>

          <button
            onClick={handlePrintAll}
            disabled={!printer || isPrinting}
            className="px-6 py-2.5 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:bg-gray-300 text-sm font-medium whitespace-nowrap"
          >
            Print All ({items.reduce((sum, i) => sum + i.quantity, 0)} labels)
          </button>
        </div>
      )}

      {/* Items list */}
      <div className="space-y-3">
        {items.map(item => (
          <div
            key={item.itemKey}
            className="bg-white border border-gray-200 rounded-lg p-3 flex flex-col md:flex-row md:items-center gap-3"
          >
            {/* Info */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2">
                <span className="font-bold text-sm">{item.itemKey}</span>
                {item.localization && (
                  <span className="text-xs text-gray-500 bg-gray-100 px-2 py-0.5 rounded">
                    {item.localization}
                  </span>
                )}
                {partInfoMap[item.itemKey]?.stockQuantity != null && (
                  <span className="text-xs text-orange-700 bg-orange-50 px-2 py-0.5 rounded">
                    Stock: {partInfoMap[item.itemKey].stockQuantity}
                  </span>
                )}
              </div>
              <p
                className="text-sm text-gray-600 mt-1 truncate"
                dir={item.language === 'ar' || item.language === 'he' ? 'rtl' : 'ltr'}
              >
                {item.description}
              </p>
            </div>

            {/* Preview thumbnail */}
            <div className="shrink-0 hidden md:block">
              <LabelPreview
                itemKey={item.itemKey}
                language={item.language}
                refreshKey={previewRefreshKeys[item.itemKey]}
              />
            </div>

            {/* Controls */}
            <div className="flex items-center gap-2 flex-wrap shrink-0">
              <div className="flex rounded-lg overflow-hidden border border-gray-300">
                <button
                  onClick={() => handleItemLanguage(item.itemKey, 'he')}
                  className={`px-2 py-1 text-xs ${item.language === 'he' ? 'bg-blue-600 text-white' : 'bg-white'}`}
                >
                  HE
                </button>
                <button
                  onClick={() => handleItemLanguage(item.itemKey, 'ar')}
                  className={`px-2 py-1 text-xs ${item.language === 'ar' ? 'bg-blue-600 text-white' : 'bg-white'}`}
                >
                  AR
                </button>
              </div>

              {item.language === 'ar' && (
                <button
                  onClick={() => setEditingArabic(item.itemKey)}
                  className="text-xs text-blue-600 hover:underline"
                >
                  Edit AR
                </button>
              )}

              <input
                type="number"
                min={1}
                value={item.quantity || ''}
                onChange={e => handleItemQuantity(item.itemKey, parseInt(e.target.value) || 0)}
                onBlur={() => handleItemQuantityBlur(item.itemKey)}
                className="w-16 border border-gray-300 rounded px-2 py-1 text-xs text-center"
              />

              <button
                onClick={() => handlePrintItem(item)}
                disabled={!printer || printingItem === item.itemKey}
                className="px-3 py-1.5 bg-green-600 text-white rounded text-xs hover:bg-green-700 disabled:bg-gray-300"
              >
                {printingItem === item.itemKey ? '...' : 'Print'}
              </button>

              <button
                onClick={() => handleRemoveItem(item.itemKey)}
                className="px-2 py-1.5 text-red-500 hover:bg-red-50 rounded text-xs"
                title="Remove"
              >
                X
              </button>
            </div>
          </div>
        ))}
      </div>

      {items.length === 0 && stockInfo && !loading && (
        <p className="text-center text-gray-400 py-8 text-sm">No items in this stock move</p>
      )}

      {/* Arabic Editor Modal */}
      {editingArabic && (
        <ArabicEditor
          itemKey={editingArabic}
          currentDescription={partInfoMap[editingArabic]?.arabicDescription ?? ''}
          onSaved={desc => handleArabicSaved(editingArabic, desc)}
          onDeleted={() => handleArabicDeleted(editingArabic)}
          onClose={() => setEditingArabic(null)}
        />
      )}

      {/* Batch Print Progress */}
      <BatchPrintProgress
        progress={progress}
        result={result}
        error={hubError}
        onClose={reset}
      />
    </div>
  );
}

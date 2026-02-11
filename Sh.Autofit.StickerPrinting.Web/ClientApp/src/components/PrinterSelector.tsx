import { useEffect, useState } from 'react';
import { getPrinters } from '../api/client';
import type { PrinterInfo } from '../types';

interface Props {
  value: string;
  onChange: (name: string) => void;
}

export default function PrinterSelector({ value, onChange }: Props) {
  const [printers, setPrinters] = useState<PrinterInfo[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getPrinters()
      .then(list => {
        setPrinters(list);
        if (!value && list.length > 0) {
          onChange(list[0].name);
        }
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  if (loading) return <span className="text-sm text-gray-400">Loading printers...</span>;
  if (printers.length === 0) return <span className="text-sm text-red-500">No printers found</span>;

  return (
    <select
      value={value}
      onChange={e => onChange(e.target.value)}
      className="w-full border border-gray-300 rounded-lg px-3 py-2.5 text-sm bg-white focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
    >
      {printers.map(p => (
        <option key={p.name} value={p.name}>
          {p.name} ({p.statusMessage})
        </option>
      ))}
    </select>
  );
}

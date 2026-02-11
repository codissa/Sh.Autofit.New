import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import PrintOnDemand from './pages/PrintOnDemand';
import StockMove from './pages/StockMove';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<PrintOnDemand />} />
          <Route path="/stock" element={<StockMove />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

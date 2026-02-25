import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import KanbanBoard from './components/KanbanBoard/KanbanBoard';
import DeliveryMethodsPage from './pages/DeliveryMethodsPage';
import CustomerRulesPage from './pages/CustomerRulesPage';
import ArchivePage from './pages/ArchivePage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<KanbanBoard />} />
          <Route path="/delivery-methods" element={<DeliveryMethodsPage />} />
          <Route path="/customer-rules" element={<CustomerRulesPage />} />
          <Route path="/archive" element={<ArchivePage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

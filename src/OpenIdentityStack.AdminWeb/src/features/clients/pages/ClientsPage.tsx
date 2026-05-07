/**
 * ClientsPage Component
 * 
 * Main page for listing clients
 */

import { ClientList } from '../components/ClientList';

export function ClientsPage() {
  return (
    <div className="container mx-auto py-6">
      <ClientList />
    </div>
  );
}

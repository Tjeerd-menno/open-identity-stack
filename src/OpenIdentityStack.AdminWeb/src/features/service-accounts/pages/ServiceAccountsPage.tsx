/**
 * ServiceAccountsPage Component
 * 
 * Main page for listing service accounts
 */

import { ServiceAccountList } from '../components/ServiceAccountList';

export function ServiceAccountsPage() {
  return (
    <div className="container mx-auto py-6">
      <ServiceAccountList />
    </div>
  );
}

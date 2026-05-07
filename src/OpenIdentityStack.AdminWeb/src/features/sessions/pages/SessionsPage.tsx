/**
 * SessionsPage Component
 * 
 * Main page for listing sessions
 */

import { SessionList } from '../components/SessionList';

export function SessionsPage() {
  return (
    <div className="container mx-auto py-6">
      <SessionList />
    </div>
  );
}

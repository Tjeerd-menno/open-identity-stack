/**
 * GroupsPage Component
 * 
 * Main page for displaying and managing groups
 */

import { GroupList } from '../components/GroupList';

export function GroupsPage() {
  return (
    <div className="container mx-auto py-6">
      <GroupList />
    </div>
  );
}

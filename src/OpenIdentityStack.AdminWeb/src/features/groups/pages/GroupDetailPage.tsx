/**
 * GroupDetailPage Component
 * 
 * Page for viewing a single group's details
 */

import { useParams } from 'react-router-dom';
import { GroupDetail } from '../components/GroupDetail';

export function GroupDetailPage() {
  const { id } = useParams<{ id: string }>();

  if (!id) {
    return <div>Group ID not provided</div>;
  }

  return (
    <div className="container mx-auto py-6">
      <GroupDetail />
    </div>
  );
}

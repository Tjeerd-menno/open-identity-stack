/**
 * SessionDetailPage Component
 * 
 * Page for viewing session details
 */

import { useParams } from 'react-router-dom';
import { SessionDetail } from '../components/SessionDetail';

export function SessionDetailPage() {
  const { id } = useParams<{ id: string }>();

  if (!id) {
    return <div>Session ID not provided</div>;
  }

  return (
    <div className="container mx-auto py-6">
      <SessionDetail />
    </div>
  );
}

/**
 * ServiceAccountDetailPage Component
 * 
 * Page for viewing service account details
 */

import { useParams } from 'react-router-dom';
import { ServiceAccountDetail } from '../components/ServiceAccountDetail';

export function ServiceAccountDetailPage() {
  const { id } = useParams<{ id: string }>();

  if (!id) {
    return <div>Service Account ID not provided</div>;
  }

  return (
    <div className="container mx-auto py-6">
      <ServiceAccountDetail />
    </div>
  );
}

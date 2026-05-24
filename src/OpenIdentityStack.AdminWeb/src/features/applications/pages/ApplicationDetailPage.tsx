import { useParams } from 'react-router-dom';
import { ApplicationDetail } from '../components/ApplicationDetail';

export function ApplicationDetailPage() {
  const { id } = useParams<{ id: string }>();

  if (!id) {
    return <div>Application ID not provided</div>;
  }

  return (
    <div className="container mx-auto py-6">
      <ApplicationDetail />
    </div>
  );
}

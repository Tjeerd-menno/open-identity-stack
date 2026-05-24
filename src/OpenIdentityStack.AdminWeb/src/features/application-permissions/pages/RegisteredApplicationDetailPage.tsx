import { useParams } from 'react-router-dom';
import { RegisteredApplicationDetail } from '../components';

export function RegisteredApplicationDetailPage() {
  const { id } = useParams<{ id: string }>();
  if (!id) {
    return <div>Service ID not provided.</div>;
  }

  return (
    <div className="container mx-auto py-6">
      <RegisteredApplicationDetail id={id} />
    </div>
  );
}

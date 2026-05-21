import { useParams } from 'react-router-dom';
import { RegisteredServiceDetail } from '../components';

export function RegisteredServiceDetailPage() {
  const { id } = useParams<{ id: string }>();
  if (!id) {
    return <div>Service ID not provided.</div>;
  }

  return (
    <div className="container mx-auto py-6">
      <RegisteredServiceDetail id={id} />
    </div>
  );
}

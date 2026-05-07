/**
 * ClientDetailPage Component
 * 
 * Page for viewing client details
 */

import { useParams } from 'react-router-dom';
import { ClientDetail } from '../components/ClientDetail';

export function ClientDetailPage() {
  const { id } = useParams<{ id: string }>();

  if (!id) {
    return <div>Client ID not provided</div>;
  }

  return (
    <div className="container mx-auto py-6">
      <ClientDetail />
    </div>
  );
}

import { ArrowLeft } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { Button } from '@/components/ui/button';
import { ApplicationForm, type UpdateApplicationFormData } from '../components/ApplicationForm';
import { useApplication } from '../hooks/useApplication';
import { useUpdateApplication } from '../hooks/useUpdateApplication';

export function EditApplicationPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: application, isLoading } = useApplication(id!);
  const updateApplication = useUpdateApplication();

  const handleSubmit = async (data: UpdateApplicationFormData) => {
    try {
      await updateApplication.mutateAsync({
        applicationId: id!,
        mode: 'metadata',
        displayName: data.displayName,
        description: data.description || null,
      });
      navigate(`/applications/${id}`);
    } catch (error) {
      console.error('Failed to update application:', error);
      throw error;
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <LoadingSpinner />
      </div>
    );
  }

  if (!application) {
    return (
      <div className="space-y-4">
        <h1 className="text-3xl font-bold">Application Not Found</h1>
        <Button onClick={() => navigate('/applications')}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back to Applications
        </Button>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 max-w-4xl">
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/applications/${id}`)}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Edit Application</h1>
            <p className="text-muted-foreground">{application.clientId}</p>
          </div>
        </div>

        <ApplicationForm
          application={application}
          onSubmit={handleSubmit}
          isLoading={updateApplication.isPending}
        />
      </div>
    </div>
  );
}

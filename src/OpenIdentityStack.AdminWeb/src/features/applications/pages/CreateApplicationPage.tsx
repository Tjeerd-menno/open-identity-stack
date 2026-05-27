import { ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { ApplicationForm, type CreateApplicationFormData } from '../components/ApplicationForm';
import { useCreateApplication } from '../hooks/useCreateApplication';

export function CreateApplicationPage() {
  const navigate = useNavigate();
  const createApplication = useCreateApplication();

  const handleSubmit = async (data: CreateApplicationFormData) => {
    try {
      const response = await createApplication.mutateAsync({
        ...data,
        description: data.description || null,
      });
      navigate(`/applications/${response.id}`);
    } catch (error) {
      console.error('Failed to create application:', error);
      throw error;
    }
  };

  return (
    <div className="container mx-auto py-6 max-w-4xl">
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate('/applications')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Create Application</h1>
            <p className="text-muted-foreground">
              Create a unified OAuth 2.0 / OpenID Connect application
            </p>
          </div>
        </div>

        <ApplicationForm
          onSubmit={handleSubmit}
          isLoading={createApplication.isPending}
        />
      </div>
    </div>
  );
}

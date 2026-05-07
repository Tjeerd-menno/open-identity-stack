/**
 * CreateGroupPage Component
 * 
 * Page for creating a new group
 */

import { useNavigate } from 'react-router-dom';
import { GroupForm } from '../components/GroupForm';
import { useCreateGroup } from '../hooks/useCreateGroup';
import type { CreateGroupRequest } from '@/types';

export function CreateGroupPage() {
  const navigate = useNavigate();
  const createGroup = useCreateGroup();

  const handleSubmit = async (data: CreateGroupRequest) => {
    try {
      const group = await createGroup.mutateAsync(data);
      navigate(`/groups/${group.id}`);
    } catch (error) {
      console.error('Failed to create group:', error);
    }
  };

  return (
    <div className="container mx-auto py-6 max-w-2xl">
      <h1 className="text-3xl font-bold mb-6">Create Group</h1>
      <GroupForm onSubmit={handleSubmit} />
    </div>
  );
}

import React, { useState } from 'react';
import { useCreateLink } from '../hooks/useLinks';
import { Plus } from 'lucide-react';

export const CreateLinkForm = () => {
    const [url, setUrl] = useState('');
    const createLink = useCreateLink();

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        if (!url) return;
        createLink.mutate(url, {
            onSuccess: () => setUrl('')
        });
    };

    return (
        <form onSubmit={handleSubmit} className="mb-8 flex gap-4">
            <input 
                type="url" 
                required
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="Enter original long URL (https://...)" 
                className="flex-1 rounded-md border-gray-300 shadow-sm px-4 py-2 border focus:ring-blue-500 focus:border-blue-500"
            />
            <button 
                type="submit" 
                disabled={createLink.isPending}
                className="bg-blue-600 text-white px-6 py-2 rounded-md hover:bg-blue-700 flex items-center gap-2 disabled:opacity-50"
            >
                <Plus size={20} />
                {createLink.isPending ? 'Creating...' : 'Shorten'}
            </button>
        </form>
    );
};

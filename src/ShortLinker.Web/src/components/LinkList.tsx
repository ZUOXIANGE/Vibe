import { useLinks } from '../hooks/useLinks';
import { format } from 'date-fns';
import { ExternalLink, Copy } from 'lucide-react';

export const LinkList = () => {
    const { data: links, isLoading, error } = useLinks();

    if (isLoading) return <div className="text-center py-8">Loading...</div>;
    if (error) return <div className="text-red-500 py-8">Error loading links</div>;
    if (!links?.length) return <div className="text-center py-8 text-gray-500">No links found. Create one above!</div>;

    return (
        <div className="bg-white shadow rounded-lg overflow-hidden">
            <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                    <tr>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Short Code</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Original URL</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Created At</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                    </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                    {links.map((link) => (
                        <tr key={link.id}>
                            <td className="px-6 py-4 whitespace-nowrap font-medium text-blue-600 flex items-center gap-2">
                                /{link.shortCode}
                                <button className="text-gray-400 hover:text-gray-600"><Copy size={16}/></button>
                            </td>
                            <td className="px-6 py-4 whitespace-nowrap text-gray-500 truncate max-w-xs">
                                <a href={link.originalUrl} target="_blank" rel="noreferrer" className="hover:underline flex items-center gap-1">
                                    {link.originalUrl} <ExternalLink size={14}/>
                                </a>
                            </td>
                            <td className="px-6 py-4 whitespace-nowrap text-gray-500">
                                {format(new Date(link.createdAt), 'MMM d, yyyy HH:mm')}
                            </td>
                            <td className="px-6 py-4 whitespace-nowrap">
                                <span className={`px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${link.isActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}`}>
                                    {link.isActive ? 'Active' : 'Disabled'}
                                </span>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
};

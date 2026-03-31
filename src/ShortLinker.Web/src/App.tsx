import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CreateLinkForm } from './components/CreateLinkForm';
import { LinkList } from './components/LinkList';

const queryClient = new QueryClient({
    defaultOptions: { queries: { refetchOnWindowFocus: false, retry: 1 } }
});

function App() {
    return (
        <QueryClientProvider client={queryClient}>
            <div className="min-h-screen bg-gray-50">
                <header className="bg-white shadow-sm p-4 border-b">
                    <div className="max-w-7xl mx-auto flex justify-between items-center">
                        <h1 className="text-2xl font-bold text-gray-900">ShortLinker</h1>
                        <span className="text-sm text-gray-500 bg-gray-100 px-3 py-1 rounded-full">
                            Tenant: {localStorage.getItem('tenant_id') || 'default'}
                        </span>
                    </div>
                </header>
                <main className="p-6 max-w-7xl mx-auto">
                    <CreateLinkForm />
                    <LinkList />
                </main>
            </div>
        </QueryClientProvider>
    );
}

export default App;

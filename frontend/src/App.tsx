import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './context/AuthContext'
import { Spinner } from './components/ui'
import DashboardLayout from './components/DashboardLayout'
import LoginPage from './pages/LoginPage'
import OnboardingPage from './pages/OnboardingPage'
import DashboardHome from './pages/DashboardHome'
import SetupWizard from './pages/SetupWizard'
import ProfilePage from './pages/ProfilePage'
import KnowledgeBasePage from './pages/KnowledgeBasePage'
import SmartPhonePage from './pages/SmartPhonePage'
import VoicePage from './pages/VoicePage'
import CallsPage from './pages/CallsPage'
import AdminPage from './pages/admin/AdminPage'

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, loading, me } = useAuth()
  if (loading) return <Spinner />
  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (!me?.profileCompleted) return <Navigate to="/onboarding" replace />
  return <>{children}</>
}

function RequireAdmin({ children }: { children: React.ReactNode }) {
  const { me, loading } = useAuth()
  if (loading) return <Spinner />
  if (me?.role !== 'SuperAdmin') return <Navigate to="/" replace />
  return <>{children}</>
}

export default function App() {
  const { loading } = useAuth()
  if (loading) return <Spinner />

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/onboarding" element={<OnboardingPage />} />
      <Route
        element={
          <RequireAuth>
            <DashboardLayout />
          </RequireAuth>
        }
      >
        <Route index element={<DashboardHome />} />
        <Route path="setup" element={<SetupWizard />} />
        <Route path="profile" element={<ProfilePage />} />
        <Route path="knowledge-base" element={<KnowledgeBasePage />} />
        <Route path="smartphone" element={<SmartPhonePage />} />
        <Route path="voice" element={<VoicePage />} />
        <Route path="calls" element={<CallsPage />} />
        <Route
          path="admin"
          element={
            <RequireAdmin>
              <AdminPage />
            </RequireAdmin>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

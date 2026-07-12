import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './context/AuthContext'
import { Spinner } from './components/ui'
import DashboardLayout from './components/DashboardLayout'
import LoginPage from './pages/LoginPage'
import OnboardingPage from './pages/OnboardingPage'
import DashboardHome from './pages/DashboardHome'
import Placeholder from './pages/Placeholder'

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
        <Route path="knowledge-base" element={<Placeholder title="پایگاه دانش" phase="فاز ۳" />} />
        <Route path="voice" element={<Placeholder title="صدای گوینده" phase="فاز ۳" />} />
        <Route path="calls" element={<Placeholder title="تماس‌ها" phase="فاز ۶" />} />
        <Route
          path="admin"
          element={
            <RequireAdmin>
              <Placeholder title="پنل سوپرادمین" phase="فاز ۴" />
            </RequireAdmin>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

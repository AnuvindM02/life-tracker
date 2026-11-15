import { Link } from "react-router";
import { EmptyState } from "@/components/common";

export function NotFoundPage() {
  return (
    <EmptyState title="Nothing here yet">
      This page either does not exist or arrives in a later milestone. <Link to="/" className="text-primary underline">Back to Today</Link>
    </EmptyState>
  );
}

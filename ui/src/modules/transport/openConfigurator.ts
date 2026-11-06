/** Opening the elevator configurator on an elevator. */

import { useUi } from '../../app/store';
import { useElevatorDialog } from './elevator';

export function openConfigurator(id: string) {
  useElevatorDialog.setState({ id });
  useUi.setState({ dialog: 'elevator' });
}

import { Search } from 'lucide-react'
import { TextInput, type TextInputProps } from './TextInput'

type SearchInputProps = Omit<
  TextInputProps,
  'type' | 'leadingIcon' | 'value' | 'defaultValue' | 'onChange'
> & {
  value: string
  onChange: NonNullable<TextInputProps['onChange']>
}

export function SearchInput(props: SearchInputProps) {
  return <TextInput {...props} type="search" leadingIcon={Search} />
}
